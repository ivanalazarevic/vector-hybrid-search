import type { ApiClient } from "./client";
import type { ApiError } from "./types";

const DEFAULT_TITLES: Record<number, string> = {
  400: "The request is not valid",
  502: "The search engine returned an error",
  503: "The search engine is not reachable",
};

/**
 * The backend answers 400 with `{ error }` and 502/503 with ProblemDetails (`title`, `detail`, `status`).
 * Both, and anything unexpected, become an ApiError.
 */
export async function readApiError(response: Response): Promise<ApiError> {
  const fallbackTitle = DEFAULT_TITLES[response.status] ?? `The API answered with status ${response.status}`;

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return { status: response.status, title: fallbackTitle };
  }

  if (typeof body !== "object" || body === null) {
    return { status: response.status, title: fallbackTitle };
  }

  const { error, title, detail } = body as Record<string, unknown>;
  if (typeof error === "string" && error !== "") {
    return { status: response.status, title: error };
  }

  return {
    status: response.status,
    title: typeof title === "string" && title !== "" ? title : fallbackTitle,
    detail: typeof detail === "string" && detail !== "" ? detail : undefined,
  };
}

async function request<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, { ...init, headers: { Accept: "application/json", ...init.headers } });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === "AbortError") {
      throw cause;
    }
    const error: ApiError = {
      status: 0,
      title: "The API is not reachable",
      detail: "No response from the backend. Check that the API is running on http://localhost:5135.",
    };
    throw error;
  }

  if (!response.ok) {
    throw await readApiError(response);
  }

  return (await response.json()) as T;
}

function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>(path, { method: "GET", signal });
}

function post<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal,
  });
}

export const httpClient: ApiClient = {
  search: (searchRequest, signal) => post("/api/search", searchRequest, signal),
  compare: (compareRequest, signal) => post("/api/search/compare", compareRequest, signal),
  listRuns: (signal) => get("/api/evaluation/runs", signal),
  getRun: (runId, signal) => get(`/api/evaluation/runs/${encodeURIComponent(runId)}`, signal),
  getStorage: (signal) => get("/api/analytics/storage", signal),
};
