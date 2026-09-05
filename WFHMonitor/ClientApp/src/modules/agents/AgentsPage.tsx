import {
  DataTable,
  MetricGrid,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { EmptyState } from "../../components/PageStates";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";

interface AgentOfficeModel {
  ceoName: string;
  ceoEmail: string;
  teamCount: number;
  agentCount: number;
  employeeCount: number;
  workingAgentCount: number;
  activeTaskCount: number;
  generatedAtUtc: string;
  agents: Array<{
    userId: string;
    name: string;
    email: string;
    teamName: string;
    workspaceName: string;
    isWorking: boolean;
    isOnline: boolean;
    lastActivityAt?: string | null;
    activeTaskCount: number;
    taskSummary: string;
    accentColor: string;
  }>;
  employees: Array<{
    userId: string;
    name: string;
    role: string;
    teamName: string;
    isOnline: boolean;
    lastActivityAt?: string | null;
    accentColor: string;
  }>;
}

export function AgentsPage() {
  const page = usePage<AgentOfficeModel>(apiRoutes.agents.index);

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          <PageHeader
            eyebrow="Automation floor"
            title="Agent activity"
            description={`A current view of automated and human work across ${page.data.teamCount} teams.`}
          />
          <MetricGrid
            metrics={[
              {
                label: "Agents",
                value: page.data.agentCount,
                icon: "bi-cpu"
              },
              {
                label: "Working now",
                value: page.data.workingAgentCount,
                icon: "bi-lightning-charge",
                tone: "accent"
              },
              {
                label: "Team members",
                value: page.data.employeeCount,
                icon: "bi-people"
              },
              {
                label: "Active assignments",
                value: page.data.activeTaskCount,
                icon: "bi-list-task"
              }
            ]}
          />
          {page.data.agents.length === 0 ? (
            <EmptyState description="Configured AI automation agents will appear here when they receive work." />
          ) : (
            <section className="office-grid">
              {page.data.agents.map((agent) => (
                <article className="surface office-person" key={agent.userId}>
                  <span
                    className="office-avatar"
                    style={
                      {
                        "--avatar-accent": agent.accentColor
                      } as React.CSSProperties
                    }
                  >
                    {agent.name.slice(0, 2).toUpperCase()}
                    <i className={agent.isOnline ? "online" : ""} />
                  </span>
                  <div>
                    <header>
                      <h2>{agent.name}</h2>
                      <StatusChip
                        value={agent.isWorking ? "In progress" : "Idle"}
                      />
                    </header>
                    <p>{agent.taskSummary}</p>
                    <small>
                      {agent.teamName} · {agent.activeTaskCount} active
                    </small>
                  </div>
                </article>
              ))}
            </section>
          )}
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">People</span>
                <h2>Team presence</h2>
              </div>
            </div>
            <DataTable
              rows={page.data.employees}
              rowKey={(employee) => employee.userId}
              columns={[
                { key: "name", label: "Name" },
                { key: "role", label: "Role" },
                { key: "teamName", label: "Team" },
                {
                  key: "isOnline",
                  label: "Presence",
                  render: (employee) => (
                    <StatusChip
                      value={employee.isOnline ? "Active" : "Offline"}
                    />
                  )
                },
                { key: "lastActivityAt", label: "Last active" }
              ]}
            />
          </section>
        </>
      )}
    </PageBoundary>
  );
}
