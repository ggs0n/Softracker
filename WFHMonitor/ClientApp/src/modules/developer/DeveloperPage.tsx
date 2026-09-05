import {
  DataTable,
  DetailGrid,
  MetricGrid,
  PageHeader
} from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import type { UnknownRecord } from "../../types/api";

interface DeveloperModel extends UnknownRecord {
  assignedBugCount: number;
  assignedChangeRequestCount: number;
  linkedBranchCount: number;
  recentCommitCount: number;
  developerEmail: string;
  totalXpPoints: number;
  assignedBugs: UnknownRecord[];
  assignedChangeRequests: UnknownRecord[];
  recentCommits: UnknownRecord[];
  branches: UnknownRecord[];
}

export function DeveloperPage() {
  const page = usePage<DeveloperModel>(apiRoutes.developer.summary);

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          <PageHeader
            eyebrow="Engineering"
            title="Developer summary"
            description={page.data.developerEmail}
          />
          <MetricGrid
            metrics={[
              {
                label: "XP points",
                value: page.data.totalXpPoints,
                icon: "bi-lightning-charge",
                tone: "accent"
              },
              {
                label: "Assigned bugs",
                value: page.data.assignedBugCount,
                icon: "bi-bug"
              },
              {
                label: "Assigned projects",
                value: page.data.assignedChangeRequestCount,
                icon: "bi-kanban"
              },
              {
                label: "Recent commits",
                value: page.data.recentCommitCount,
                icon: "bi-git"
              }
            ]}
          />
          <section className="surface detail-surface">
            <DetailGrid
              record={page.data}
              omit={[
                "assignedBugs",
                "assignedChangeRequests",
                "recentCommits",
                "branches"
              ]}
            />
          </section>
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">GitHub</span>
                <h2>Recent commits</h2>
              </div>
            </div>
            <DataTable
              rows={page.data.recentCommits}
              rowKey={(commit) => String(commit.sha)}
              columns={[
                { key: "repo", label: "Repository" },
                { key: "branch", label: "Branch" },
                { key: "message", label: "Commit" },
                { key: "date", label: "Date" }
              ]}
            />
          </section>
        </>
      )}
    </PageBoundary>
  );
}
