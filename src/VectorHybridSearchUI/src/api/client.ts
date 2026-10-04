import type {
  ApiError,
  CompareRequest,
  EngineStorageStats,
  EvaluationRun,
  EvaluationRunSummary,
  SearchComparisonResponse,
  SearchRequest,
  SearchResponse,
} from "./types";

/** Implemented twice: by the mock client and by the http client. Every method rejects with an ApiError. */
export interface ApiClient {
  search(request: SearchRequest, signal?: AbortSignal): Promise<SearchResponse>;
  compare(request: CompareRequest, signal?: AbortSignal): Promise<SearchComparisonResponse>;
  listRuns(signal?: AbortSignal): Promise<EvaluationRunSummary[]>;
  getRun(runId: string, signal?: AbortSignal): Promise<EvaluationRun>;
  getStorage(signal?: AbortSignal): Promise<EngineStorageStats[]>;
}

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as ApiError).status === "number" &&
    typeof (value as ApiError).title === "string"
  );
}

/** For rendering: anything thrown that is not already an ApiError is a bug in the UI, not an API failure. */
export function toApiError(value: unknown): ApiError {
  if (isApiError(value)) {
    return value;
  }
  return {
    status: 0,
    title: "Something went wrong in the page",
    detail: value instanceof Error ? value.message : String(value),
  };
}
