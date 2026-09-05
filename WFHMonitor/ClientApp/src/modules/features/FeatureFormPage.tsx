import {
  EntityForm,
  enumOptions,
  type FormDefinition
} from "../../components/EntityForm";
import { apiRoutes } from "../../services/microservices";

const featureDefinition: FormDefinition = {
  eyebrow: "Delivery scope",
  createTitle: "Create feature",
  editTitle: "Edit feature",
  description: "Capture the change, affected module, ownership, and target.",
  getEndpoint: (id, projectId) =>
    id
      ? apiRoutes.changeRequests.editFeature(id)
      : apiRoutes.changeRequests.createFeature(projectId),
  postEndpoint: (id) =>
    id
      ? apiRoutes.changeRequests.updateFeature
      : apiRoutes.changeRequests.createFeature(),
  listPath: "/features",
  detailPath: (id) => `/features/${id}`,
  fields: [
    {
      name: "changeRequestId",
      label: "Project",
      type: "select",
      required: true,
      optionsFrom: "projectOptions"
    },
    {
      name: "name",
      label: "Feature title",
      required: true,
      maxLength: 200,
      span: "full"
    },
    {
      name: "description",
      label: "Description",
      type: "textarea",
      maxLength: 500,
      span: "full"
    },
    { name: "moduleImpacted", label: "Affected module", maxLength: 200 },
    { name: "linkedBugs", label: "Linked bugs", maxLength: 1000 },
    {
      name: "pullRequestUrl",
      label: "Pull request",
      type: "url",
      maxLength: 500
    },
    {
      name: "assignedDeveloperId",
      label: "Assigned to",
      type: "select",
      optionsFrom: "developerOptions"
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
    { name: "timelineStart", label: "Start date", type: "date" },
    { name: "timelineEnd", label: "Target date", type: "date" }
  ]
};

export function FeatureFormPage() {
  return <EntityForm definition={featureDefinition} />;
}
