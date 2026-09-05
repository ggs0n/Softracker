import { useState } from "react";
import {
  DataTable,
  DetailGrid,
  MetricGrid,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
import { EvidencePanel } from "../../components/EvidencePanel";
import { PageBoundary } from "../../components/PageBoundary";
import { Notice } from "../../components/PageStates";
import { ServerAction } from "../../components/ServerAction";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams
} from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";
import type { BugReport } from "../../types/api";

export function BugsPage() {
  const { session } = useSession();
  const [search] = useSearchParams();
  const status = search.get("status");
  const endpoint = apiRoutes.bugs.index(status);
  const page = usePage<BugReport[]>(endpoint);

  return (
    <PageBoundary {...page}>
      <PageHeader
        eyebrow="Quality"
        title="Bug tracker"
        description="Triage defects, ownership, and automated fixes."
        actions={
          session?.access.canModifyBugs && (
            <Link className="button button-primary" to="/bugs/new">
              <i className="bi bi-plus-lg" /> Report bug
            </Link>
          )
        }
      />
      <section className="surface">
        <DataTable
          rows={page.data ?? []}
          rowKey={(bug) => bug.id}
          linkForRow={(bug) => `/bugs/${bug.id}`}
          columns={[
            { key: "bugNumber", label: "Number" },
            { key: "title", label: "Bug" },
            {
              key: "severity",
              label: "Severity",
              render: (bug) => <StatusChip value={bug.severity} />
            },
            {
              key: "status",
              label: "Status",
              render: (bug) => <StatusChip value={bug.status} />
            },
            { key: "moduleImpacted", label: "Module" },
            { key: "changeRequest", label: "Project" },
            { key: "assignedDeveloper", label: "Owner" },
            {
              key: "agentStatus",
              label: "Agent",
              render: (bug) => <StatusChip value={bug.agentStatus} />
            }
          ]}
        />
      </section>
    </PageBoundary>
  );
}

export function BugDetailsPage() {
  const { id = "" } = useParams();
  const { session } = useSession();
  const page = usePage<BugReport>(apiRoutes.bugs.details(id));
  const navigate = useNavigate();
  const [message, setMessage] = useState<string | null>(null);
  const bug = page.data;

  return (
    <PageBoundary {...page}>
      {bug && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow={bug.bugNumber}
            title={bug.title}
            description={
              bug.description ?? "No bug description has been added."
            }
            actions={
              session?.access.canModifyBugs && (
                <Link
                  className="button button-secondary"
                  to={`/bugs/${id}/edit`}
                >
                  <i className="bi bi-pencil" /> Edit
                </Link>
              )
            }
          />
          <MetricGrid
            metrics={[
              {
                label: "Status",
                value: <StatusChip value={bug.status} />,
                icon: "bi-flag"
              },
              {
                label: "Severity",
                value: <StatusChip value={bug.severity} />,
                icon: "bi-exclamation-triangle"
              },
              {
                label: "Module",
                value: bug.moduleImpacted ?? "—",
                icon: "bi-box"
              },
              {
                label: "Agent",
                value: <StatusChip value={bug.agentStatus} />,
                icon: "bi-cpu"
              }
            ]}
          />
          <section className="surface detail-surface">
            <DetailGrid
              record={bug}
              omit={[
                "description",
                "activities",
                "screenshots",
                "documents"
              ]}
            />
          </section>
          <section className="surface prose-columns">
            <article>
              <span className="eyebrow">Workflow</span>
              <h2>Affected flow</h2>
              <p>{bug.workflow ?? "No workflow notes."}</p>
            </article>
            <article>
              <span className="eyebrow">Reproduction</span>
              <h2>Steps to reproduce</h2>
              <p>{bug.stepsToReproduce ?? "No reproduction steps."}</p>
            </article>
          </section>
          <div className="evidence-layout">
            <EvidencePanel
              eyebrow="Evidence"
              title="Screenshots"
              items={bug.screenshots ?? []}
              fileBaseUrl="/uploads/bugs/screenshots"
              image
              canManage={
                session?.roles.some(
                  (role) => role === "Admin" || role === "Tester"
                ) ?? false
              }
              uploadEndpoint={apiRoutes.bugs.uploadScreenshot}
              uploadValues={{ id, returnUrl: `/app/bugs/${id}` }}
              deleteEndpoint={apiRoutes.bugs.deleteScreenshot}
              deleteValues={(item) => ({ screenshotId: item.id, bugId: id })}
              onComplete={(response) => {
                setMessage(response.success ?? "Bug evidence updated.");
                void page.reload();
              }}
            />
            <EvidencePanel
              eyebrow="Files"
              title="Supporting documents"
              items={bug.documents ?? []}
              fileBaseUrl="/uploads/bugs/docs"
              canManage={
                session?.roles.some(
                  (role) => role === "Admin" || role === "Tester"
                ) ?? false
              }
              uploadEndpoint={apiRoutes.bugs.uploadDocument}
              uploadValues={{ id, returnUrl: `/app/bugs/${id}` }}
              deleteEndpoint={apiRoutes.bugs.deleteDocument}
              deleteValues={(item) => ({ documentId: item.id, bugId: id })}
              onComplete={(response) => {
                setMessage(response.success ?? "Bug evidence updated.");
                void page.reload();
              }}
            />
          </div>
          {session?.access.canModifyBugs && (
            <section className="surface action-strip">
              <div>
                <h2>Bug workflow</h2>
                <p>Queue a fix or move the ticket through triage.</p>
              </div>
              <ServerAction
                endpoint={apiRoutes.bugs.fixWithAgent(id)}
                values={{ id }}
                onComplete={(response) => {
                  setMessage(response.success ?? "Bug queued.");
                  void page.reload();
                }}
              >
                <i className="bi bi-cpu" /> Fix with agent
              </ServerAction>
              <ServerAction
                endpoint={apiRoutes.bugs.updateStatus(id)}
                values={{ id, status: "Done" }}
                onComplete={(response) => {
                  setMessage(response.success ?? "Bug updated.");
                  void page.reload();
                }}
              >
                <i className="bi bi-check2-circle" /> Mark done
              </ServerAction>
              {session.roles.some(
                (role) => role === "Admin" || role === "Tester"
              ) && (
                <ServerAction
                  endpoint={apiRoutes.bugs.delete(id)}
                  values={{ id }}
                  className="text-button danger"
                  confirmMessage={`Delete ${bug.bugNumber}?`}
                  onComplete={() => navigate("/bugs")}
                >
                  Delete bug
                </ServerAction>
              )}
            </section>
          )}
        </>
      )}
    </PageBoundary>
  );
}
