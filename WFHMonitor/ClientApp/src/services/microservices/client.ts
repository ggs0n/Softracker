import type {
  ApiEnvelope,
  BootstrapResponse,
  UnknownRecord
} from "../../types/api";
import { apiRoutes } from "./endpoints";

export class ApiError extends Error {
  readonly status: number;
  readonly errors: Record<string, string[]>;

  constructor(
    message: string,
    status: number,
    errors: Record<string, string[]> = {}
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors;
  }
}

async function readJson<T>(response: Response): Promise<T> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    throw new ApiError("The server returned an unexpected response.", response.status);
  }

  return (await response.json()) as T;
}

async function send<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: "include",
    ...init,
    headers: {
      Accept: "application/json",
      ...init?.headers
    }
  });

  if (response.status === 401)
    throw new ApiError("Your session has expired. Sign in again.", 401);
  if (response.status === 403)
    throw new ApiError("You do not have access to this operation.", 403);
  if (response.status === 204 && response.ok)
    return undefined as T;

  const payload = await readJson<T | ApiEnvelope<unknown>>(response);
  if (!response.ok) {
    const envelope = payload as Partial<ApiEnvelope<unknown>>;
    const problem = payload as { title?: string; detail?: string };
    throw new ApiError(
      envelope.error ?? problem.detail ?? problem.title ?? "The request failed.",
      response.status,
      envelope.errors ?? {}
    );
  }

  return payload as T;
}

export function getBootstrap(): Promise<BootstrapResponse> {
  return send<BootstrapResponse>(apiRoutes.spa.bootstrap);
}

export function getPage<T>(path: string, signal?: AbortSignal): Promise<ApiEnvelope<T>> {
  return send<ApiEnvelope<T> | T>(path, { signal }).then((payload) => {
    if (
      payload !== null &&
      typeof payload === "object" &&
      "data" in payload &&
      "errors" in payload
    ) {
      return payload as ApiEnvelope<T>;
    }

    return {
      data: payload as T,
      errors: {}
    };
  });
}

function appendValue(
  target: URLSearchParams | FormData,
  key: string,
  value: unknown
): void {
  if (value === null || value === undefined) return;

  if (value instanceof File) {
    if (target instanceof FormData) target.append(key, value);
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) => appendValue(target, `${key}[${index}]`, item));
    return;
  }

  if (typeof value === "object") {
    Object.entries(value as UnknownRecord).forEach(([childKey, childValue]) =>
      appendValue(target, key ? `${key}.${childKey}` : childKey, childValue)
    );
    return;
  }

  target.append(key, String(value));
}

function toPayload(
  values: UnknownRecord,
  useMultipart: boolean
): URLSearchParams | FormData {
  const target = useMultipart ? new FormData() : new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => appendValue(target, key, value));
  return target;
}

export function postForm<T = unknown>(
  path: string,
  values: UnknownRecord,
  antiForgeryToken: string,
  useMultipart = false
): Promise<ApiEnvelope<T>> {
  const body = toPayload(values, useMultipart);
  const headers: Record<string, string> = {
    "X-CSRF-TOKEN": antiForgeryToken
  };
  if (!useMultipart)
    headers["Content-Type"] = "application/x-www-form-urlencoded;charset=UTF-8";

  return send<ApiEnvelope<T>>(path, {
    method: "POST",
    body,
    headers
  }).then((response) => {
    if (response.error)
      throw new ApiError(response.error, 400, response.errors);
    return response;
  });
}

export function postJson<T>(
  path: string,
  values: unknown,
  antiForgeryToken: string
): Promise<T> {
  return send<T>(path, {
    method: "POST",
    body: JSON.stringify(values),
    headers: {
      "Content-Type": "application/json;charset=UTF-8",
      "X-CSRF-TOKEN": antiForgeryToken
    }
  });
}

export function deleteRequest(
  path: string,
  antiForgeryToken: string
): Promise<void> {
  return send<void>(path, {
    method: "DELETE",
    headers: {
      "X-CSRF-TOKEN": antiForgeryToken
    }
  });
}
