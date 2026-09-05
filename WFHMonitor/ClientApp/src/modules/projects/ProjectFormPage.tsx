import {
  EntityForm,
  enumOptions,
  type FormDefinition
} from "../../components/EntityForm";
import { apiRoutes } from "../../services/microservices";

const projectDefinition: FormDefinition = {
  eyebrow: "Portfolio",
  createTitle: "Create project",
  editTitle: "Edit project",
  description: "Define scope, delivery dates, and repository context.",
  getEndpoint: (id) =>
    id ? apiRoutes.changeRequests.edit(id) : apiRoutes.changeRequests.create,
  postEndpoint: (id) =>
    id ? apiRoutes.changeRequests.edit(id) : apiRoutes.changeRequests.create,
  listPath: "/projects",
  detailPath: (id) => `/projects/${id}`,
  fields: [
    {
      name: "title",
      label: "Project title",
      required: true,
      maxLength: 300,
      span: "full"
    },
    {
      name: "description",
      label: "Summary",
      type: "textarea",
      maxLength: 4000,
      span: "full"
    },
    {
      name: "status",
      label: "Status",
      type: "select",
      options: enumOptions(
        "Draft",
        "InReview",
        "Approved",
        "InProgress",
        "Done",
        "Rejected"
      )
    },
    {
      name: "priority",
      label: "Priority",
      type: "select",
      options: enumOptions("Low", "Medium", "High", "Critical")
    },
    {
      name: "stage",
      label: "Stage",
      type: "select",
      options: enumOptions(
        "ProjectStart",
        "Development",
        "Testing",
        "Deploy",
        "DeploymentComplete"
      )
    },
    {
      name: "technologyStack",
      label: "Technology / languages",
      maxLength: 800
    },
    { name: "timelineStart", label: "Start date", type: "date" },
    { name: "timelineEnd", label: "Target date", type: "date" },
    {
      name: "figmaLink",
      label: "Figma link",
      type: "url",
      maxLength: 500
    },
    {
      name: "archSpecLink",
      label: "Architecture spec link",
      type: "url",
      maxLength: 500
    },
    {
      name: "archSpecNotes",
      label: "Architecture notes",
      type: "textarea",
      maxLength: 2000,
      span: "full"
    },
    {
      name: "githubRepoUrl",
      label: "GitHub repository",
      type: "url",
      maxLength: 500
    },
    { name: "githubBranch", label: "Branch", maxLength: 100 }
  ]
};

export function ProjectFormPage() {
  return <EntityForm definition={projectDefinition} />;
}
