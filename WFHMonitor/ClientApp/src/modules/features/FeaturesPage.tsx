import { useMemo, useState } from "react";
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
import { Link, useNavigate, useParams } from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";
import type {
  ChangeRequest,
  ProjectFeature
} from "../../types/api";

export function FeaturesPage() {
  const { session } = useSession();
  const page = usePage<ChangeRequest[]>(apiRoutes.changeRequests.features);
  const features = useMemo(
    () =>
      (page.data ?? []).flatMap((project) =>
        (project.features ?? []).map((feature) => ({ ...feature, project }))
      ),
    [page.data]
  );

  return (
    <PageBoundary {...page}>
      <PageHeader
        eyebrow="Delivery scope"
        title="Features"
        description="A cross-project view of planned and active product work."
        actions={
          session?.access.canModifyFeatures && (
            <Link className="button button-primary" to="/features/new">
              <i className="bi bi-plus-lg" /> New feature
            </Link>
          )
        }
      />
      <section className="surface">
        <DataTable
          rows={features}
          rowKey={(feature) => feature.id}
          linkForRow={(feature) => `/features/${feature.id}`}
          columns={[
            { key: "featureNumber", label: "Number" },
            { key: "name", label: "Feature" },
            {
              key: "project",
              label: "Project",
              render: (feature) =>
                `${feature.project.crNumber} · ${feature.project.title}`
            },
            {
              key: "priority",
              label: "Priority",
              render: (feature) => <StatusChip value={feature.priority} />
            },
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
            { key: "assignedDeveloper", label: "Owner" }
          ]}
        />
      </section>
    </PageBoundary>
  );
}

export function FeatureDetailsPage() {
  const { id = "" } = useParams();
  const { session } = useSession();
  const page = usePage<ProjectFeature>(
    apiRoutes.changeRequests.featureDetails(id)
  );
  const navigate = useNavigate();
  const [message, setMessage] = useState<string | null>(null);
  const feature = page.data;

  return (
    <PageBoundary {...page}>
      {feature && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow={feature.featureNumber}
            title={feature.name}
            description={
              feature.description ?? "No feature description has been added."
            }
            actions={
              session?.access.canModifyFeatures && (
                <Link
                  className="button button-secondary"
                  to={`/features/${id}/edit`}
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
                value: <StatusChip value={feature.status} />,
                icon: "bi-flag"
              },
              {
                label: "Stage",
                value: <StatusChip value={feature.stage} />,
                icon: "bi-signpost-split"
              },
              {
                label: "Priority",
                value: <StatusChip value={feature.priority} />,
                icon: "bi-arrow-up-right"
              },
              {
                label: "Agent",
                value: <StatusChip value={feature.agentStatus} />,
                icon: "bi-cpu"
              }
            ]}
          />
          <section className="surface detail-surface">
            <DetailGrid
              record={feature}
              omit={["description", "screenshots", "agentImplementationPlan"]}
            />
          </section>
          {feature.agentImplementationPlan && (
            <section className="surface prose-panel">
              <span className="eyebrow">Implementation plan</span>
              <h2>Automation notes</h2>
              <pre>{feature.agentImplementationPlan}</pre>
            </section>
          )}
          <EvidencePanel
            eyebrow="Evidence"
            title="Fix screenshots"
            items={feature.screenshots ?? []}
            fileBaseUrl="/uploads/features/screenshots"
            image
            canManage={Boolean(
              session?.roles.some(
                (role) => role === "Admin" || role === "Agent"
              )
              || (session?.roles.includes("Developer")
                && feature.assignedDeveloperId === session.user?.id)
            )}
            uploadEndpoint={apiRoutes.changeRequests.uploadFeatureScreenshot}
            uploadValues={{
              featureId: feature.id,
              returnUrl: `/app/features/${id}`
            }}
            deleteEndpoint={apiRoutes.changeRequests.deleteFeatureScreenshot}
            deleteValues={(item) => ({
              screenshotId: item.id,
              featureId: feature.id,
              returnUrl: `/app/features/${id}`
            })}
            onComplete={(response) => {
              setMessage(response.success ?? "Feature evidence updated.");
              void page.reload();
            }}
          />
          {session?.access.canModifyFeatures && (
            <section className="surface action-strip">
              <div>
                <h2>Feature workflow</h2>
                <p>Update ownership or completion without leaving this view.</p>
              </div>
              <ServerAction
                endpoint={apiRoutes.changeRequests.pickupFeature}
                values={{ featureId: feature.id }}
                onComplete={(response) => {
                  setMessage(response.success ?? "Feature queued.");
                  void page.reload();
                }}
              >
                <i className="bi bi-cpu" /> Pick up with agent
              </ServerAction>
              <ServerAction
                endpoint={apiRoutes.changeRequests.toggleFeature}
                values={{
                  featureId: feature.id,
                  returnUrl: `/app/features/${id}`
                }}
                onComplete={(response) => {
                  setMessage(response.success ?? "Feature updated.");
                  void page.reload();
                }}
              >
                <i className="bi bi-check2-square" /> Toggle complete
              </ServerAction>
              {session.roles.includes("Admin") && (
                <ServerAction
                  endpoint={apiRoutes.changeRequests.deleteFeature}
                  values={{
                    featureId: feature.id,
                    returnUrl: "/app/features"
                  }}
                  className="text-button danger"
                  confirmMessage={`Delete ${feature.featureNumber}?`}
                  onComplete={() => navigate("/features")}
                >
                  Delete feature
                </ServerAction>
              )}
            </section>
          )}
        </>
      )}
    </PageBoundary>
  );
}
