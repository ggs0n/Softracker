import { useState } from "react";
import {
  DataTable,
  DetailGrid,
  MetricGrid,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
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
import type {
  BugReport,
  ChangeRequest,
  TestCase
} from "../../types/api";
import { CoverageCard } from "./components/CoverageCard";

interface QaIndexModel {
  testCases: TestCase[];
  projects: ChangeRequest[];
  linkedBugs: BugReport[];
  relatedBugCounts: Record<string, number>;
  totalCount: number;
  passedCount: number;
  failedCount: number;
  pendingCount: number;
  skipCount: number;
  passRate: number;
  moduleCoverages: Array<{
    module: string;
    total: number;
    passed: number;
    coveragePercent: number;
  }>;
  selectedScanAgentId: string;
  aiAutomationProfileOptions: Array<{ value: string; text: string }>;
}

export function QaPage() {
  const { session } = useSession();
  const [search] = useSearchParams();
  const page = usePage<QaIndexModel>(apiRoutes.qa.index(search));
  const [message, setMessage] = useState<string | null>(null);
  const firstProject = page.data?.projects[0];

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow="Quality assurance"
            title="Test coverage"
            description="Plan module-level checks and connect failures to defects."
            actions={
              session?.access.canModifyQaTesting && (
                <Link className="button button-primary" to="/qa/new">
                  <i className="bi bi-plus-lg" /> New test case
                </Link>
              )
            }
          />
          <MetricGrid
            metrics={[
              {
                label: "Test cases",
                value: page.data.totalCount,
                icon: "bi-card-checklist"
              },
              {
                label: "Pass rate",
                value: `${page.data.passRate}%`,
                icon: "bi-check2-circle",
                tone: "accent"
              },
              {
                label: "Failed",
                value: page.data.failedCount,
                icon: "bi-x-circle",
                tone: page.data.failedCount > 0 ? "danger" : "default"
              },
              {
                label: "Pending",
                value: page.data.pendingCount,
                icon: "bi-hourglass-split"
              }
            ]}
          />
          <section className="coverage-grid">
            {page.data.moduleCoverages.map((module) => (
              <CoverageCard key={module.module} {...module} />
            ))}
          </section>
          <section className="surface">
            <DataTable
              rows={page.data.testCases}
              rowKey={(test) => test.id}
              linkForRow={(test) => `/qa/${test.id}`}
              columns={[
                { key: "testNumber", label: "Number" },
                { key: "name", label: "Test case" },
                { key: "module", label: "Module" },
                {
                  key: "category",
                  label: "Category",
                  render: (test) => <StatusChip value={test.category} />
                },
                { key: "environment", label: "Environment" },
                {
                  key: "status",
                  label: "Status",
                  render: (test) => <StatusChip value={test.status} />
                },
                { key: "changeRequest", label: "Project" }
              ]}
            />
          </section>
          {firstProject && session?.access.canModifyQaTesting && (
            <section className="surface automation-panel">
              <div>
                <span className="eyebrow">Automation</span>
                <h2>Generate coverage</h2>
                <p>
                  Use the selected project to queue test generation and scanning.
                </p>
              </div>
              <div>
                <ServerAction
                  endpoint={apiRoutes.qa.autoGenerate}
                  values={{
                    projectId: firstProject.id,
                    scanAgentId: page.data.selectedScanAgentId
                  }}
                  onComplete={(response) => {
                    setMessage(response.success ?? "Generation queued.");
                    void page.reload();
                  }}
                >
                  <i className="bi bi-stars" /> Generate tests
                </ServerAction>
                <ServerAction
                  endpoint={apiRoutes.qa.scanAndGenerate}
                  values={{
                    projectId: firstProject.id,
                    scanAgentId: page.data.selectedScanAgentId
                  }}
                  onComplete={(response) => {
                    setMessage(response.success ?? "Scan queued.");
                    void page.reload();
                  }}
                >
                  <i className="bi bi-shield-check" /> Scan and generate
                </ServerAction>
              </div>
            </section>
          )}
        </>
      )}
    </PageBoundary>
  );
}

export function QaDetailsPage() {
  const { id = "" } = useParams();
  const page = usePage<{ testCase: TestCase; relatedBugs: BugReport[] }>(
    apiRoutes.qa.details(id)
  );
  const { session } = useSession();
  const navigate = useNavigate();
  const [message, setMessage] = useState<string | null>(null);
  const model = page.data;

  return (
    <PageBoundary {...page}>
      {model && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow={model.testCase.testNumber}
            title={model.testCase.name}
            description={
              model.testCase.description ?? "No test notes have been added."
            }
            actions={
              session?.access.canModifyQaTesting && (
                <Link
                  className="button button-secondary"
                  to={`/qa/${id}/edit`}
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
                value: <StatusChip value={model.testCase.status} />,
                icon: "bi-flag"
              },
              {
                label: "Category",
                value: <StatusChip value={model.testCase.category} />,
                icon: "bi-tags"
              },
              {
                label: "Environment",
                value: model.testCase.environment,
                icon: "bi-hdd-stack"
              },
              {
                label: "Related bugs",
                value: model.relatedBugs.length,
                icon: "bi-bug"
              }
            ]}
          />
          <section className="surface detail-surface">
            <DetailGrid record={model.testCase} omit={["description"]} />
          </section>
          {session?.access.canModifyQaTesting && (
            <section className="surface action-strip">
              <div>
                <span className="eyebrow">Run result</span>
                <h2>Update test status</h2>
              </div>
              <div>
                {["Pass", "Fail", "Pending", "Skip"].map((status) => (
                  <ServerAction
                    endpoint={apiRoutes.qa.updateStatus}
                    values={{ id, status }}
                    key={status}
                    onComplete={(response) => {
                      setMessage(
                        response.success ?? `Test marked ${status}.`
                      );
                      void page.reload();
                    }}
                  >
                    {status}
                  </ServerAction>
                ))}
                <ServerAction
                  endpoint={apiRoutes.qa.delete}
                  values={{ id }}
                  className="text-button danger"
                  confirmMessage={`Delete ${model.testCase.testNumber}? This cannot be undone.`}
                  onComplete={() => navigate("/qa")}
                >
                  Delete
                </ServerAction>
              </div>
            </section>
          )}
          <section className="surface">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Defects</span>
                <h2>Related bugs</h2>
              </div>
            </div>
            <DataTable
              rows={model.relatedBugs}
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
                }
              ]}
            />
          </section>
        </>
      )}
    </PageBoundary>
  );
}
