import {
  EntityForm,
  enumOptions,
  type FormDefinition
} from "../../components/EntityForm";
import { apiRoutes } from "../../services/microservices";

const bugDefinition: FormDefinition = {
  eyebrow: "Quality",
  createTitle: "Report bug",
  editTitle: "Edit bug",
  description:
    "Give the team enough context to reproduce, triage, and resolve the defect.",
  getEndpoint: (id) => (id ? apiRoutes.bugs.edit(id) : apiRoutes.bugs.create),
  postEndpoint: (id) => (id ? apiRoutes.bugs.edit(id) : apiRoutes.bugs.create),
  listPath: "/bugs",
  detailPath: (id) => `/bugs/${id}`,
  fields: [
    {
      name: "title",
      label: "Bug title",
      required: true,
      maxLength: 300,
      span: "full"
    },
    {
      name: "description",
      label: "Description",
      type: "textarea",
      maxLength: 4000,
      span: "full"
    },
    {
      name: "workflow",
      label: "Affected workflow",
      type: "textarea",
      maxLength: 4000,
      span: "full"
    },
    {
      name: "stepsToReproduce",
      label: "Steps to reproduce",
      type: "textarea",
      maxLength: 4000,
      span: "full"
    },
    { name: "moduleImpacted", label: "Affected module", maxLength: 200 },
    {
      name: "severity",
      label: "Severity",
      type: "select",
      options: enumOptions("Low", "Medium", "High", "Critical")
    },
    {
      name: "status",
      label: "Status",
      type: "select",
      options: enumOptions(
        "New",
        "Triaged",
        "InProgress",
        "Fixed",
        "Retest",
        "Done",
        "Reopened",
        "Closed"
      )
    },
    {
      name: "assigneeType",
      label: "Assignee type",
      type: "select",
      options: enumOptions("Developer", "Agent")
    },
    {
      name: "assignedDeveloperId",
      label: "Developer",
      type: "select",
      optionsFrom: "developerOptions"
    },
    {
      name: "assignedAgentId",
      label: "Agent",
      type: "select",
      optionsFrom: "agentOptions"
    },
    {
      name: "changeRequestId",
      label: "Project",
      type: "select",
      optionsFrom: "changeRequestOptions"
    },
    {
      name: "changeRequestReferenceText",
      label: "Project reference",
      maxLength: 300
    },
    {
      name: "pullRequestUrl",
      label: "Pull request",
      type: "url",
      maxLength: 500
    },
    {
      name: "documentFile",
      label: "Supporting document",
      type: "file",
      help: "Attach a relevant document up to the server upload limit."
    }
  ]
};

export function BugFormPage() {
  return <EntityForm definition={bugDefinition} />;
}
