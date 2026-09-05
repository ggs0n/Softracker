import { useEffect, useState, type FormEvent } from "react";
import { PageHeader } from "../../components/DataDisplay";
import {
  enumOptions,
  type FormField
} from "../../components/EntityForm";
import {
  EmptyState,
  ErrorState,
  LoadingState,
  Notice
} from "../../components/PageStates";
import { usePage } from "../../hooks/usePage";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { Link, useNavigate, useParams } from "../../state/RouterContext";
import { useSession } from "../../state/SessionContext";
import type {
  BugReport,
  ChangeRequest,
  UnknownRecord
} from "../../types/api";

interface QaNewModel {
  projects: ChangeRequest[];
  linkedBugs: BugReport[];
}

interface EditableTestCase {
  id: number;
  name: string;
  description?: string | null;
  module?: string | null;
  status: string;
  category: string;
  environment: string;
  changeRequestId?: number | null;
  linkedBugId?: number | null;
}

export function TestCaseFormPage() {
  const { id } = useParams();
  const page = usePage<QaNewModel>(apiRoutes.qa.index());
  const details = usePage<{ testCase: EditableTestCase }>(
    id ? apiRoutes.qa.details(id) : null
  );
  const { session } = useSession();
  const navigate = useNavigate();
  const [values, setValues] = useState<UnknownRecord>({
    name: "",
    description: "",
    module: "",
    status: "Pending",
    category: "Regression",
    environment: "Dev",
    changeRequestId: "",
    linkedBugId: ""
  });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!id || !details.data) return;
    const testCase = details.data.testCase;
    setValues({
      id: testCase.id,
      name: testCase.name,
      description: testCase.description ?? "",
      module: testCase.module ?? "",
      status: testCase.status,
      category: testCase.category,
      environment: testCase.environment,
      changeRequestId: testCase.changeRequestId ?? "",
      linkedBugId: testCase.linkedBugId ?? ""
    });
  }, [details.data, id]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setBusy(true);
      setError(null);
      await postForm(
        id ? apiRoutes.qa.edit : apiRoutes.qa.createTestCase,
        values,
        session.antiForgeryToken
      );
      navigate(id ? `/qa/${id}` : "/qa");
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "The test case could not be saved."
      );
    } finally {
      setBusy(false);
    }
  }

  if (page.loading || details.loading) return <LoadingState />;
  if (page.error)
    return <ErrorState message={page.error} onRetry={page.reload} />;
  if (details.error)
    return <ErrorState message={details.error} onRetry={details.reload} />;
  if (!page.data)
    return <EmptyState description="Test case options are not available." />;

  const fields: FormField[] = [
    {
      name: "name",
      label: "Test case name",
      required: true,
      span: "full"
    },
    {
      name: "description",
      label: "Purpose, steps, and expected outcome",
      type: "textarea",
      span: "full"
    },
    { name: "module", label: "Module" },
    {
      name: "status",
      label: "Status",
      type: "select",
      options: enumOptions("Pass", "Fail", "Pending", "Skip")
    },
    {
      name: "category",
      label: "Category",
      type: "select",
      options: enumOptions("Smoke", "Regression", "UAT")
    },
    {
      name: "environment",
      label: "Environment",
      type: "select",
      options: enumOptions("Dev", "Staging", "UAT", "Production")
    },
    {
      name: "changeRequestId",
      label: "Project",
      type: "select",
      options: page.data.projects.map((project) => ({
        value: String(project.id),
        text: `${project.crNumber} · ${project.title}`
      }))
    },
    {
      name: "linkedBugId",
      label: "Linked bug",
      type: "select",
      options: page.data.linkedBugs.map((bug) => ({
        value: String(bug.id),
        text: `${bug.bugNumber} · ${bug.title}`
      }))
    }
  ];

  return (
    <>
      <PageHeader
        eyebrow="Quality assurance"
        title={id ? "Edit test case" : "Create test case"}
        description="Cover a complete module or important end-to-end flow."
      />
      <form
        className="surface entity-form"
        onSubmit={(event) => void submit(event)}
      >
        {error && <Notice kind="error">{error}</Notice>}
        <div className="form-grid">
          {fields.map((field) => (
            <label
              className={
                field.span === "full"
                  ? "form-field form-field-full"
                  : "form-field"
              }
              key={field.name}
            >
              <span>{field.label}</span>
              {field.type === "textarea" ? (
                <textarea
                  rows={6}
                  value={String(values[field.name] ?? "")}
                  onChange={(event) =>
                    setValues((current) => ({
                      ...current,
                      [field.name]: event.target.value
                    }))}
                />
              ) : field.type === "select" ? (
                <select
                  value={String(values[field.name] ?? "")}
                  onChange={(event) =>
                    setValues((current) => ({
                      ...current,
                      [field.name]: event.target.value
                    }))}
                >
                  <option value="">
                    Select {field.label.toLowerCase()}
                  </option>
                  {field.options?.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.text}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  required={field.required}
                  value={String(values[field.name] ?? "")}
                  onChange={(event) =>
                    setValues((current) => ({
                      ...current,
                      [field.name]: event.target.value
                    }))}
                />
              )}
            </label>
          ))}
        </div>
        <footer className="form-footer">
          <Link className="button button-tertiary" to="/qa">
            Cancel
          </Link>
          <button
            className="button button-primary"
            disabled={busy}
            type="submit"
          >
            {id ? "Save changes" : "Create test case"}
          </button>
        </footer>
      </form>
    </>
  );
}
