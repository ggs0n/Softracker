import type {
  BatchJob,
  CostEstimate,
  DeploymentService,
  DesignProjectDetails,
  GenerateDesignRequest,
  GrpcContract,
  RestEndpoint,
  ServiceDetail,
  TableSchema
} from "../types";

export async function copyText(value: string): Promise<void> {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.append(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
}

export function buildChatGptPrompt(form: GenerateDesignRequest): string {
  return `You are a senior system design architect.

Create a practical system design kickstart blueprint for this system.

Simple summary of system:
${form.summary}

Technology use:
${form.technology}

Cloud / hosting target:
${form.cloudHostingTarget.trim() || "No preference. Decide the best target from the technology, scale, features, and prototype cost."}

How many users use:
${form.userCount}

Main features:
${form.features}

Return ONLY valid JSON without Markdown or code fences. Match this shape:

{
  "title": "short system name",
  "recommendedArchitecture": "practical architecture recommendation",
  "mainComponents": ["component 1", "component 2"],
  "deploymentServices": [{
    "module": "API Gateway / Backend API",
    "recommendedService": "concrete hosting service",
    "runtime": "runtime",
    "reason": "why it fits"
  }],
  "serviceDetails": [{
    "name": "Orders Service",
    "type": "Microservice",
    "responsibility": "owns the order lifecycle",
    "runtime": ".NET 8",
    "deployment": "container service",
    "dataOwnership": "orders and order items",
    "dependencies": ["Payments Service"],
    "communication": ["REST", "asynchronous events"]
  }],
  "restEndpoints": [{
    "service": "Orders Service",
    "method": "POST",
    "path": "/api/v1/orders",
    "purpose": "create an order",
    "authentication": "authenticated customer",
    "request": "CreateOrderRequest JSON",
    "response": "OrderResponse JSON",
    "statusCodes": ["201", "400", "401", "409"]
  }],
  "grpcContracts": [{
    "service": "Payments Service",
    "contract": "payments.v1.PaymentService",
    "rpcMethod": "AuthorizePayment",
    "requestMessage": "AuthorizePaymentRequest",
    "responseMessage": "AuthorizePaymentResponse",
    "streaming": "Unary",
    "purpose": "low-latency internal payment authorization"
  }],
  "batchJobs": [{
    "name": "Outbox dispatcher",
    "ownerService": "Orders Service",
    "trigger": "Timer and queue",
    "schedule": "Every minute",
    "responsibility": "publish committed integration events",
    "input": "unpublished outbox rows",
    "output": "published events and attempt records",
    "retryPolicy": "exponential backoff then dead-letter",
    "idempotencyStrategy": "outbox event id"
  }],
  "databaseStorageRecommendation": "database and storage recommendation",
  "apiBackendRecommendation": "API/backend recommendation",
  "scalingAdvice": "advice based on user count",
  "securityNotes": "security notes",
  "costEstimate": {
    "currency": "USD",
    "monthlyRange": "$150 - $800 / month",
    "summary": "rough monthly infrastructure estimate",
    "lineItems": [{
      "name": "Compute / hosting",
      "monthlyRange": "$50 - $250",
      "notes": "what is included"
    }],
    "assumptions": ["single region"],
    "costOptimizations": ["start small"]
  },
  "risksTradeoffs": ["risk 1", "risk 2"],
  "nextSteps": ["step 1", "step 2"],
  "tableSchemas": [{
    "name": "users",
    "purpose": "stores user accounts",
    "columns": [{
      "name": "id",
      "type": "uuid",
      "isPrimaryKey": true,
      "isForeignKey": false,
      "notes": "unique user id"
    }],
    "relationships": ["referenced by business tables"]
  }],
  "diagrams": [{
    "title": "System Context",
    "kind": "context",
    "mermaid": "flowchart LR\\n  User[User] --> App[System]"
  }],
  "sourceMode": "ChatGPT Plus",
  "notice": null
}

Rules:
- Generate exactly five diagrams in this order and with these kinds: context, components, dataFlow, detailedArchitecture, cloudDeploymentTemplate.
- Include concrete deployment-service and runtime choices for every major module.
- Treat every supplied main feature as a canonical capability. Preserve its exact name in architecture, components, service/API details, schemas, risks, next steps, and every diagram; never use alternate names for the same capability.
- Describe every service or deployable module, including responsibility, data ownership, dependencies, and communication.
- Include practical versioned REST endpoints with authentication, request/response contracts, and status codes.
- Include gRPC contracts only when an internal RPC boundary is justified; otherwise return an empty grpcContracts array.
- Include scheduled or queue-triggered batch jobs with retry and idempotency details.
- Generate three to six starter database tables with types, PK/FK flags, and relationships.
- Include a rough monthly USD cost with line items, assumptions, and optimizations.
- The detailedArchitecture diagram must show clients, authentication, verified and rejected paths, API gateway, internal services, data, and a legend.
- The cloudDeploymentTemplate diagram must show a cloud boundary, product account, managed/shared account, authentication, numbered flows, hosting/CDN/storage/API/compute/database/queue/logging/secrets/registry areas, and concrete provider labels.
- Mermaid fields contain Mermaid source only, escaped as JSON strings.
- Keep the answer concise and useful for a real prototype.`;
}

export function downloadMarkdown(design: DesignProjectDetails): void {
  downloadText(
    `${slugify(design.blueprint.title)}.md`,
    toMarkdown(design),
    "text/markdown;charset=utf-8"
  );
}

export function toMarkdown(design: DesignProjectDetails): string {
  const blueprint = design.blueprint;
  const diagrams = blueprint.diagrams
    .map(
      (diagram) =>
        `### ${diagram.title}\n\n\`\`\`mermaid\n${diagram.mermaid}\n\`\`\``
    )
    .join("\n\n");

  return `# ${blueprint.title}

## Input

- Summary: ${design.summary}
- Technology: ${design.technology}
- Cloud / hosting target: ${design.cloudHostingTarget?.trim() || "App decides"}
- Users: ${design.userCount}
- Features: ${design.features}

## Recommended Architecture

${blueprint.recommendedArchitecture}

## Main Components

${blueprint.mainComponents.map((item) => `- ${item}`).join("\n")}

## Module Deployment Services

${deploymentServicesToMarkdown(blueprint.deploymentServices ?? [])}

## Services and Data Ownership

${serviceDetailsToMarkdown(blueprint.serviceDetails ?? [])}

## REST API Endpoints

${restEndpointsToMarkdown(blueprint.restEndpoints ?? [])}

## gRPC Contracts

${grpcContractsToMarkdown(blueprint.grpcContracts ?? [])}

## Batch and Scheduled Jobs

${batchJobsToMarkdown(blueprint.batchJobs ?? [])}

## Database and Storage

${blueprint.databaseStorageRecommendation}

## Total Estimated Cost

${blueprint.costEstimate ? costEstimateToMarkdown(blueprint.costEstimate) : "No estimate available."}

## Table Schema

${(blueprint.tableSchemas ?? []).map(tableSchemaToMarkdown).join("\n\n") || "No schema available."}

## API and Backend

${blueprint.apiBackendRecommendation}

## Scaling Advice

${blueprint.scalingAdvice}

## Security Notes

${blueprint.securityNotes}

## Risks and Tradeoffs

${blueprint.risksTradeoffs.map((item) => `- ${item}`).join("\n")}

## Next Steps

${blueprint.nextSteps.map((item) => `- ${item}`).join("\n")}

## Diagrams

${diagrams}
`;
}

export function deploymentServicesToMarkdown(
  services: DeploymentService[]
): string {
  if (services.length === 0)
    return "No deployment service mapping available.";

  const rows = services
    .map(
      (service) =>
        `| ${cleanCell(service.module)} | ${cleanCell(service.recommendedService)} | ${cleanCell(service.runtime)} | ${cleanCell(service.reason)} |`
    )
    .join("\n");
  return `| Module | Service | Runtime | Why |
| --- | --- | --- | --- |
${rows}`;
}

export function serviceDetailsToMarkdown(
  services: ServiceDetail[]
): string {
  if (services.length === 0) return "No service details available.";
  return services
    .map((service) => `### ${service.name}

- Type: ${service.type}
- Runtime: ${service.runtime}
- Deployment: ${service.deployment}
- Responsibility: ${service.responsibility}
- Data ownership: ${service.dataOwnership}
- Dependencies: ${service.dependencies.join(", ") || "None"}
- Communication: ${service.communication.join(", ") || "In-process"}`)
    .join("\n\n");
}

export function restEndpointsToMarkdown(
  endpoints: RestEndpoint[]
): string {
  if (endpoints.length === 0) return "No REST endpoints recommended.";
  const rows = endpoints
    .map((endpoint) =>
      `| ${endpoint.method} | \`${cleanCell(endpoint.path)}\` | ${cleanCell(endpoint.service)} | ${cleanCell(endpoint.purpose)} | ${cleanCell(endpoint.authentication)} | ${cleanCell(endpoint.request)} | ${cleanCell(endpoint.response)} | ${endpoint.statusCodes.join(", ")} |`)
    .join("\n");
  return `| Method | Path | Service | Purpose | Authentication | Request | Response | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
${rows}`;
}

export function grpcContractsToMarkdown(
  contracts: GrpcContract[]
): string {
  if (contracts.length === 0) return "No gRPC boundary recommended.";
  const rows = contracts
    .map((contract) =>
      `| ${cleanCell(contract.service)} | \`${cleanCell(contract.contract)}/${cleanCell(contract.rpcMethod)}\` | ${cleanCell(contract.requestMessage)} | ${cleanCell(contract.responseMessage)} | ${cleanCell(contract.streaming)} | ${cleanCell(contract.purpose)} |`)
    .join("\n");
  return `| Service | RPC | Request | Response | Streaming | Purpose |
| --- | --- | --- | --- | --- | --- |
${rows}`;
}

export function batchJobsToMarkdown(jobs: BatchJob[]): string {
  if (jobs.length === 0) return "No batch jobs recommended.";
  return jobs
    .map((job) => `### ${job.name}

- Owner: ${job.ownerService}
- Trigger: ${job.trigger}
- Schedule: ${job.schedule}
- Responsibility: ${job.responsibility}
- Input: ${job.input}
- Output: ${job.output}
- Retry policy: ${job.retryPolicy}
- Idempotency: ${job.idempotencyStrategy}`)
    .join("\n\n");
}

export function costEstimateToMarkdown(cost: CostEstimate): string {
  const rows = cost.lineItems
    .map(
      (item) =>
        `| ${cleanCell(item.name)} | ${cleanCell(item.monthlyRange)} | ${cleanCell(item.notes)} |`
    )
    .join("\n");
  return `${cost.monthlyRange} ${cost.currency}

${cost.summary}

| Item | Monthly | Notes |
| --- | --- | --- |
${rows}

Assumptions:
${cost.assumptions.map((item) => `- ${item}`).join("\n")}

Cost optimizations:
${cost.costOptimizations.map((item) => `- ${item}`).join("\n")}`;
}

export function tableSchemaToMarkdown(schema: TableSchema): string {
  const rows = schema.columns
    .map(
      (column) =>
        `| ${cleanCell(column.name)} | ${cleanCell(column.type)} | ${column.isPrimaryKey ? "PK" : column.isForeignKey ? "FK" : "-"} | ${cleanCell(column.notes)} |`
    )
    .join("\n");
  return `### ${schema.name}

${schema.purpose}

| Column | Type | Key | Notes |
| --- | --- | --- | --- |
${rows}

Relationships:
${schema.relationships.map((item) => `- ${item}`).join("\n")}`;
}

export function tableSchemasToDbml(schemas: TableSchema[]): string {
  return schemas
    .map((schema) => {
      const columns = schema.columns
        .map((column) => {
          const flags = [
            column.isPrimaryKey ? "pk" : "",
            column.isForeignKey ? "ref" : ""
          ].filter(Boolean);
          return `  ${column.name} ${shortType(column.type)}${flags.length ? ` [${flags.join(", ")}]` : ""}`;
        })
        .join("\n");
      return `Table ${schema.name} {\n${columns}\n}`;
    })
    .join("\n\n");
}

export function shortType(value: string): string {
  const lower = value.toLowerCase();
  if (lower.includes("uuid")) return "uuid";
  if (lower.includes("int")) return "int";
  if (lower.includes("date") || lower.includes("time")) return "datetime";
  if (lower.includes("bool")) return "bool";
  if (lower.includes("decimal") || lower.includes("money")) return "decimal";
  return lower.includes("text") ? "text" : "varchar";
}

function cleanCell(value: string): string {
  return value.replace(/\|/g, "\\|").replace(/\r?\n/g, " ");
}

function slugify(value: string): string {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/(^-|-$)/g, "")
      .slice(0, 60) || "system-design"
  );
}

function downloadText(fileName: string, value: string, type: string): void {
  const blob = new Blob([value], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
