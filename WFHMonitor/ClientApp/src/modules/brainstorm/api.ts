import {
  apiRoutes,
  deleteRequest,
  getPage,
  postJson
} from "../../services/microservices";
import type {
  DesignProjectDetails,
  DesignProjectSummary,
  AiAccountStatus,
  AiLoginStartResult,
  AiLoginStatus,
  ModuleImageGenerationResult,
  GenerateDesignRequest,
  ImportChatGptResultRequest
} from "./types";

export async function getAiAccountStatus(
  signal?: AbortSignal
): Promise<AiAccountStatus> {
  const response = await getPage<AiAccountStatus>(
    apiRoutes.aiAccount.status,
    signal
  );
  return response.data;
}

export function connectAiAccount(
  antiForgeryToken: string
): Promise<AiLoginStartResult> {
  return postJson<AiLoginStartResult>(
    apiRoutes.aiAccount.connect,
    {},
    antiForgeryToken
  );
}

export async function getAiLoginStatus(
  loginId: string
): Promise<AiLoginStatus> {
  const response = await getPage<AiLoginStatus>(
    apiRoutes.aiAccount.loginStatus(loginId)
  );
  return response.data;
}

export function cancelAiLogin(
  loginId: string,
  antiForgeryToken: string
): Promise<void> {
  return postJson<void>(
    apiRoutes.aiAccount.cancel(loginId),
    {},
    antiForgeryToken
  );
}

export function logoutAiAccount(
  antiForgeryToken: string
): Promise<void> {
  return postJson<void>(
    apiRoutes.aiAccount.logout,
    {},
    antiForgeryToken
  );
}

export async function listDesigns(
  signal?: AbortSignal
): Promise<DesignProjectSummary[]> {
  const response = await getPage<DesignProjectSummary[]>(
    apiRoutes.brainstorm.index,
    signal
  );
  return response.data;
}

export async function getDesign(
  id: number,
  signal?: AbortSignal
): Promise<DesignProjectDetails> {
  const response = await getPage<DesignProjectDetails>(
    apiRoutes.brainstorm.details(id),
    signal
  );
  return response.data;
}

export function generateDesign(
  request: GenerateDesignRequest,
  antiForgeryToken: string
): Promise<DesignProjectDetails> {
  return postJson<DesignProjectDetails>(
    apiRoutes.brainstorm.generate,
    request,
    antiForgeryToken
  );
}

export function importChatGptResult(
  request: ImportChatGptResultRequest,
  antiForgeryToken: string
): Promise<DesignProjectDetails> {
  return postJson<DesignProjectDetails>(
    apiRoutes.brainstorm.importChatGpt,
    request,
    antiForgeryToken
  );
}

export function generateModuleImages(
  id: number,
  antiForgeryToken: string
): Promise<ModuleImageGenerationResult> {
  return postJson<ModuleImageGenerationResult>(
    apiRoutes.brainstorm.generateModuleImages(id),
    {},
    antiForgeryToken
  );
}

export function deleteDesign(
  id: number,
  antiForgeryToken: string
): Promise<void> {
  return deleteRequest(apiRoutes.brainstorm.delete(id), antiForgeryToken);
}
