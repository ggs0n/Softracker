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
import { EmptyState, Notice } from "../../components/PageStates";
import { ServerAction } from "../../components/ServerAction";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import { Link, useNavigate, useParams } from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";
import type {
  ApiEnvelope,
  ChangeRequest
} from "../../types/api";

export function ProjectsPage() {
  const { session } = useSession();
  const page = usePage<ChangeRequest[]>(apiRoutes.changeRequests.index);

  return (
    <PageBoundary {...page}>
      <PageHeader
        eyebrow="Portfolio"
        title="All projects"
        description="Track each project from planning through deployment."
        actions={
          session?.access.canModifyAllProjects && (
            <Link className="button button-primary" to="/projects/new">
              <i className="bi bi-plus-lg" /> New project
            </Link>
          )
        }
      />
      {page.data?.length === 0 ? (
        <EmptyState
          title="Start with your first project"
          description="Add scope, owners, timeline, and a repository to begin tracking delivery."
          action={
            <Link className="button button-primary" to="/projects/new">
              Create project
            </Link>
          }
        />
      ) : (
        <section className="surface">
          <DataTable
            rows={page.data ?? []}
            rowKey={(project) => project.id}
            linkForRow={(project) => `/projects/${project.id}`}
            columns={[
              { key: "crNumber", label: "Number" },
              { key: "title", label: "Project" },
              {
                key: "status",
                label: "Status",
                render: (project) => <StatusChip value={project.status} />
              },
              {
                key: "stage",
                label: "Stage",
                render: (project) => <StatusChip value={project.stage} />
              },
              {
                key: "priority",
                label: "Priority",
                render: (project) => <StatusChip value={project.priority} />
              },
              { key: "timelineEnd", label: "Target" },
              {
                key: "bugScanStatus",
                label: "Automation",
                render: (project) => (
                  <StatusChip value={project.bugScanStatus} />
                )
              }
            ]}
          />
        </section>
      )}
    </PageBoundary>
  );
}

export function ProjectDetailsPage() {
  const { id = "" } = useParams();
  const { session } = useSession();
  const navigate = useNavigate();
  const page = usePage<ChangeRequest>(apiRoutes.changeRequests.details(id));
  const [message, setMessage] = useState<string | null>(null);
  const complete = (response: ApiEnvelope<unknown>) => {
    setMessage(response.success ?? "The operation completed.");
    void page.reload();
  };
  const project = page.data;

  return (
    <PageBoundary {...page}>
      {project && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow={project.crNumber}
            title={project.title}
            description={
              project.description ?? "No project summary has been added."
            }
            actions={
              <>
                {session?.access.canModifyAllProjects && (
                  <Link
                    className="button button-secondary"
                    to={`/projects/${id}/edit`}
                  >
                    <i className="bi bi-pencil" /> Edit
                  </Link>
                )}
                {session?.access.canModifyFeatures && (
                  <Link
                    className="button button-primary"
                    to={`/features/new?projectId=${id}`}
                  >
                    <i className="bi bi-plus-lg" /> Add feature
                  </Link>
                )}
              </>
            }
          />
          <MetricGrid
            metrics={[
              {
                label: "Status",
                value: <StatusChip value={project.status} />,
                icon: "bi-flag"
              },
              {
                label: "Stage",
                value: <StatusChip value={project.stage} />,
                icon: "bi-signpost-split"
              },
              {
                label: "Priority",
                value: <StatusChip value={project.priority} />,
                icon: "bi-arrow-up-right"
              },
              {
                label: "Health",
                value: project.projectHealthScore ?? "—",
                hint: project.projectHealthLabel ?? "Not analyzed",
                icon: "bi-heart-pulse"
              }
            ]}
          />
          <section className="surface detail-surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Project record</span>
                <h2>Scope and integration</h2>
              </div>
            </div>
            <DetailGrid
              record={project}
              omit={[
                "description",
                "features",
                "pics",
                "documents",
                "archSpecImages",
                "repositoryFeatures"
              ]}
            />
          </section>
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Features</span>
                <h2>Delivery scope</h2>
              </div>
              <span>{project.features?.length ?? 0} items</span>
            </div>
            <DataTable
              rows={project.features ?? []}
              rowKey={(feature) => feature.id}
              linkForRow={(feature) => `/features/${feature.id}`}
              columns={[
                { key: "featureNumber", label: "Number" },
                { key: "name", label: "Feature" },
                {
                  key: "stage",
                  label: "Stage",
                  render: (feature) => <StatusChip value={feature.stage} />
                },
                {
                  key: "status",
                  label: "Status",
                  render: (feature) => <StatusChip value={feature.status} />
                },
                {
                  key: "agentStatus",
                  label: "Agent",
                  render: (feature) => (
                    <StatusChip value={feature.agentStatus} />
                  )
                }
              ]}
            />
          </section>
          <div className="evidence-layout">
            <EvidencePanel
              eyebrow="Architecture"
              title="Reference images"
              items={project.archSpecImages ?? []}
              fileBaseUrl="/uploads/archspec"
              image
              canManage={session?.roles.includes("Admin") ?? false}
              uploadEndpoint={apiRoutes.changeRequests.uploadImage}
              uploadValues={{
                id,
                caption: "",
                returnUrl: `/app/projects/${id}`
              }}
              deleteEndpoint={apiRoutes.changeRequests.deleteImage}
              deleteValues={(item) => ({ imageId: item.id, crId: id })}
              onComplete={complete}
            />
            <EvidencePanel
              eyebrow="Files"
              title="Project documents"
              items={project.documents ?? []}
              fileBaseUrl="/uploads/docs"
              canManage={session?.roles.includes("Admin") ?? false}
              uploadEndpoint={apiRoutes.changeRequests.uploadDocument}
              uploadValues={{ id, returnUrl: `/app/projects/${id}` }}
              deleteEndpoint={apiRoutes.changeRequests.deleteDocument}
              deleteValues={(item) => ({ documentId: item.id, crId: id })}
              onComplete={complete}
            />
          </div>
          {session?.access.canModifyAllProjects && (
            <section className="surface automation-panel">
              <div>
                <span className="eyebrow">Automation</span>
                <h2>Engineering checks</h2>
                <p>Queue focused work in the .NET automation service.</p>
              </div>
              <div>
                <ServerAction
                  endpoint={apiRoutes.changeRequests.scanFeatures(id)}
                  values={{ id }}
                  onComplete={complete}
                >
                  <i className="bi bi-stars" /> Detect features
                </ServerAction>
                <ServerAction
                  endpoint={apiRoutes.changeRequests.findBugs(id)}
                  values={{ id }}
                  onComplete={complete}
                >
                  <i className="bi bi-bug" /> Find bugs
                </ServerAction>
                <ServerAction
                  endpoint={apiRoutes.changeRequests.analyzeHealth(id)}
                  values={{ id }}
                  onComplete={complete}
                >
                  <i className="bi bi-heart-pulse" /> Analyze health
                </ServerAction>
                <ServerAction
                  endpoint={apiRoutes.changeRequests.delete(id)}
                  values={{ id }}
                  className="text-button danger"
                  confirmMessage={`Delete ${project.crNumber}? This also removes its linked project data.`}
                  onComplete={() => navigate("/projects")}
                >
                  Delete project
                </ServerAction>
              </div>
            </section>
          )}
        </>
      )}
    </PageBoundary>
  );
}
