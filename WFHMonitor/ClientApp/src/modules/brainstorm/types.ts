export interface GenerateDesignRequest {
  summary: string;
  technology: string;
  cloudHostingTarget: string;
  userCount: string;
  features: string;
}

export interface AiAccountStatus {
  state: "connected" | "disconnected" | "unavailable" | "wrongAuthentication";
  email: string | null;
  planType: string | null;
  model: string;
  usedPercent: number | null;
  resetsAt: string | null;
  limitReachedType: string | null;
  message: string | null;
}

export interface AiLoginStartResult {
  loginId: string;
  authUrl: string;
}

export interface AiLoginStatus {
  loginId: string;
  state: "pending" | "completed" | "failed" | "cancelled" | "unknown";
  message: string | null;
}

export interface ImportChatGptResultRequest extends GenerateDesignRequest {
  chatGptResult: string;
}

export type DiagramKind =
  | "context"
  | "components"
  | "dataFlow"
  | "detailedArchitecture"
  | "cloudDeploymentTemplate";

export interface DesignDiagram {
  title: string;
  kind: DiagramKind;
  mermaid: string;
}

export interface DeploymentService {
  module: string;
  recommendedService: string;
  runtime: string;
  reason: string;
}

export interface ServiceDetail {
  name: string;
  type: string;
  responsibility: string;
  runtime: string;
  deployment: string;
  dataOwnership: string;
  dependencies: string[];
  communication: string[];
}

export interface RestEndpoint {
  service: string;
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  path: string;
  purpose: string;
  authentication: string;
  request: string;
  response: string;
  statusCodes: string[];
}

export interface GrpcContract {
  service: string;
  contract: string;
  rpcMethod: string;
  requestMessage: string;
  responseMessage: string;
  streaming:
    | "Unary"
    | "Client streaming"
    | "Server streaming"
    | "Bidirectional streaming";
  purpose: string;
}

export interface BatchJob {
  name: string;
  ownerService: string;
  trigger: string;
  schedule: string;
  responsibility: string;
  input: string;
  output: string;
  retryPolicy: string;
  idempotencyStrategy: string;
}

export interface CostLineItem {
  name: string;
  monthlyRange: string;
  notes: string;
}

export interface CostEstimate {
  currency: string;
  monthlyRange: string;
  summary: string;
  lineItems: CostLineItem[];
  assumptions: string[];
  costOptimizations: string[];
}

export interface TableColumn {
  name: string;
  type: string;
  isPrimaryKey: boolean;
  isForeignKey: boolean;
  notes: string;
}

export interface TableSchema {
  name: string;
  purpose: string;
  columns: TableColumn[];
  relationships: string[];
}

export interface DesignBlueprint {
  title: string;
  recommendedArchitecture: string;
  mainComponents: string[];
  deploymentServices: DeploymentService[] | null;
  databaseStorageRecommendation: string;
  apiBackendRecommendation: string;
  scalingAdvice: string;
  securityNotes: string;
  costEstimate: CostEstimate | null;
  risksTradeoffs: string[];
  nextSteps: string[];
  tableSchemas: TableSchema[] | null;
  diagrams: DesignDiagram[];
  sourceMode: string;
  notice: string | null;
  serviceDetails: ServiceDetail[] | null;
  restEndpoints: RestEndpoint[] | null;
  grpcContracts: GrpcContract[] | null;
  batchJobs: BatchJob[] | null;
}

export interface DesignProjectSummary {
  id: number;
  title: string;
  summary: string;
  technology: string;
  cloudHostingTarget: string | null;
  userCount: string;
  createdAt: string;
  sourceMode: string;
}

export interface DesignProjectDetails {
  id: number;
  summary: string;
  technology: string;
  cloudHostingTarget: string | null;
  userCount: string;
  features: string;
  createdAt: string;
  blueprint: DesignBlueprint;
}

export interface GeneratedModuleImage {
  moduleName: string;
  mimeType: "image/png" | "image/jpeg" | "image/webp";
  base64Data: string;
  revisedPrompt: string | null;
}

export interface ModuleImageGenerationResult {
  succeeded: boolean;
  error: string;
  images: GeneratedModuleImage[];
}

export type ResultTab =
  | "overview"
  | "architecture"
  | "services"
  | "schema"
  | "risks"
  | "diagrams"
  | "moduleImages";
