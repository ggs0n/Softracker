import {
  DataTable,
  MetricGrid,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { Progress } from "../../components/Progress";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import { Link } from "../../state/RouterContext";

interface DashboardModel {
  totalEmployees: number;
  tasksDoneTodayCount: number;
  agentFeaturesShipped: number;
  agentBugsFound: number;
  agentBugsFixed: number;
  developerDeliveredItems: number;
  totalBugs: number;
  openBugs: number;
  completeBugs: number;
  projects: Array<{
    id: number;
    crNumber: string;
    title: string;
    status: string;
    stage: string;
    priority: string;
    totalTasks: number;
    doneTasks: number;
    totalBugs: number;
    openBugs: number;
    totalFeatures: number;
    completedFeatures: number;
    taskCompletionPercent: number;
    featureCompletionPercent: number;
    isOverdue: boolean;
  }>;
  dailyTaskReports: Array<{
    date: string;
    totalCount: number;
    tasks: Array<{
      id: number;
      title: string;
      assigneeName?: string | null;
      itemType: string;
      itemNumber?: string | null;
    }>;
  }>;
}

export function DashboardPage() {
  const page = usePage<DashboardModel>(apiRoutes.admin.dashboard);

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          <PageHeader
            eyebrow="Today"
            title="Delivery overview"
            description="A live read on throughput, quality, and project risk."
          />
          <MetricGrid
            metrics={[
              {
                label: "Team members",
                value: page.data.totalEmployees,
                icon: "bi-people"
              },
              {
                label: "Done today",
                value: page.data.tasksDoneTodayCount,
                icon: "bi-check2-circle",
                tone: "accent"
              },
              {
                label: "Features shipped",
                value: page.data.agentFeaturesShipped,
                icon: "bi-box-seam"
              },
              {
                label: "Open bugs",
                value: page.data.openBugs,
                hint: `${page.data.completeBugs} resolved`,
                icon: "bi-bug",
                tone: page.data.openBugs > 0 ? "danger" : "default"
              },
              {
                label: "Agent fixes",
                value: page.data.agentBugsFixed,
                icon: "bi-cpu"
              },
              {
                label: "Developer deliveries",
                value: page.data.developerDeliveredItems,
                icon: "bi-code-square"
              }
            ]}
          />
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Portfolio</span>
                <h2>Projects in motion</h2>
              </div>
              <Link to="/projects">View all</Link>
            </div>
            <DataTable
              rows={page.data.projects}
              rowKey={(project) => project.id}
              linkForRow={(project) => `/projects/${project.id}`}
              columns={[
                { key: "crNumber", label: "Project" },
                { key: "title", label: "Title" },
                {
                  key: "stage",
                  label: "Stage",
                  render: (project) => <StatusChip value={project.stage} />
                },
                {
                  key: "taskCompletionPercent",
                  label: "Tasks",
                  render: (project) => (
                    <Progress value={project.taskCompletionPercent} />
                  )
                },
                {
                  key: "featureCompletionPercent",
                  label: "Features",
                  render: (project) => (
                    <Progress value={project.featureCompletionPercent} />
                  )
                },
                { key: "openBugs", label: "Open bugs" }
              ]}
            />
          </section>
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Activity</span>
                <h2>Recent deliveries</h2>
              </div>
            </div>
            <div className="activity-days">
              {page.data.dailyTaskReports.slice(0, 7).map((report) => (
                <article key={report.date}>
                  <header>
                    <time dateTime={report.date}>
                      {new Date(report.date).toLocaleDateString(undefined, {
                        weekday: "short",
                        day: "numeric",
                        month: "short"
                      })}
                    </time>
                    <span>{report.totalCount}</span>
                  </header>
                  {report.tasks.length === 0 ? (
                    <p>No deliveries recorded.</p>
                  ) : (
                    <ul>
                      {report.tasks.slice(0, 5).map((task) => (
                        <li key={`${task.itemType}-${task.id}`}>
                          <span>
                            <small>{task.itemNumber ?? task.itemType}</small>
                            {task.title}
                          </span>
                          <em>{task.assigneeName ?? "Unassigned"}</em>
                        </li>
                      ))}
                    </ul>
                  )}
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </PageBoundary>
  );
}
