import {
  DataTable,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import { Link } from "../../state/RouterContext";

interface MonitorModel {
  generatedAtUtc: string;
  projects: Array<{
    projectId: number;
    crNumber: string;
    title: string;
    stage: string;
    status: string;
    awsRegion: string;
    awsHealth: {
      state: string;
      label: string;
      details: string;
    };
    analyticsViewsLast30Days?: number | null;
    analyticsMessage: string;
  }>;
  stripePayments: Array<{
    id: string;
    createdAtUtc: string;
    amount: number;
    currency: string;
    status: string;
    description: string;
  }>;
  stripeMessage: string;
}

export function MonitorPage() {
  const page = usePage<MonitorModel>(apiRoutes.monitor.index);

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          <PageHeader
            eyebrow="Runtime signals"
            title="Project monitor"
            description="Cloud health, audience activity, and payment events."
          />
          <section className="monitor-grid">
            {page.data.projects.map((project) => (
              <article
                className="surface monitor-card"
                key={project.projectId}
              >
                <header>
                  <Link to={`/projects/${project.projectId}`}>
                    <small>{project.crNumber}</small>
                    <h2>{project.title}</h2>
                  </Link>
                  <StatusChip value={project.awsHealth.label} />
                </header>
                <dl>
                  <div>
                    <dt>AWS region</dt>
                    <dd>{project.awsRegion}</dd>
                  </div>
                  <div>
                    <dt>Views · 30 days</dt>
                    <dd className="number">
                      {project.analyticsViewsLast30Days?.toLocaleString()
                        ?? "—"}
                    </dd>
                  </div>
                  <div>
                    <dt>Stage</dt>
                    <dd>
                      <StatusChip value={project.stage} />
                    </dd>
                  </div>
                </dl>
                <p>{project.awsHealth.details}</p>
              </article>
            ))}
          </section>
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Billing</span>
                <h2>Recent Stripe payments</h2>
              </div>
              <span>{page.data.stripeMessage}</span>
            </div>
            <DataTable
              rows={page.data.stripePayments}
              rowKey={(payment) => payment.id}
              columns={[
                { key: "createdAtUtc", label: "Date" },
                { key: "description", label: "Description" },
                {
                  key: "amount",
                  label: "Amount",
                  render: (payment) =>
                    `${payment.currency} ${payment.amount.toFixed(2)}`
                },
                {
                  key: "status",
                  label: "Status",
                  render: (payment) => (
                    <StatusChip value={payment.status} />
                  )
                }
              ]}
            />
          </section>
        </>
      )}
    </PageBoundary>
  );
}
