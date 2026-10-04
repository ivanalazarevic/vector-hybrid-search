// Contracts shared with the backend. Kept identical to the "Contracts" section of
// .claude/plans/frontend-ui-plan.md; change both together.

export type SearchEngine = "Elasticsearch" | "MongoDbAtlas";
export type SearchMode = "Bm25" | "Vector" | "Hybrid";
export type HybridStrategy = "Rrf" | "Native";

export interface SearchFilters {
  category?: string;
  source?: string;
  publishedFrom?: string; // ISO 8601
  publishedTo?: string;
}

export interface HybridOptions {
  strategy: HybridStrategy;
  bm25Weight: number;
  vectorWeight: number;
  rrfK: number;
}

// --- exists today (hybrid, timings, bm25Rank, vectorRank arrive in backend Phases 1-2)
export interface SearchRequest {
  query: string;
  engine: SearchEngine;
  mode: SearchMode;
  topK: number; // 1-100
  filters?: SearchFilters;
  hybrid?: HybridOptions; // only when mode = Hybrid
  includeDiagnostics: boolean;
}

export interface SearchResult {
  articleId: string;
  title: string;
  snippet: string; // HTML-escaped text that may contain <em> highlights
  score: number;
  rank: number;
  source?: string;
  category?: string;
  bm25Rank?: number; // hybrid only
  vectorRank?: number; // hybrid only
}

export interface SearchTimings {
  embeddingMs: number;
  engineMs: number;
}

export interface SearchDiagnostics {
  strategy: string;
  requestedTopK: number;
  metadata: Record<string, string>;
}

export interface SearchResponse {
  queryId: string;
  query: string;
  engine: SearchEngine;
  mode: SearchMode;
  elapsedMs: number;
  timings?: SearchTimings;
  results: SearchResult[];
  diagnostics?: SearchDiagnostics;
}

// --- backend Phase 2: POST /api/search/compare
export interface CompareRequest {
  query: string;
  mode: SearchMode;
  topK: number;
  filters?: SearchFilters;
  hybrid?: HybridOptions;
  includeDiagnostics: boolean;
}

export interface EngineComparisonEntry {
  engine: SearchEngine;
  response: SearchResponse | null;
  error: string | null;
}

export interface SharedArticle {
  articleId: string;
  elasticsearchRank: number;
  mongoDbRank: number;
}

export interface SearchComparisonResponse {
  queryId: string;
  query: string;
  mode: SearchMode;
  engines: EngineComparisonEntry[];
  overlap: {
    sharedCount: number;
    jaccard: number;
    shared: SharedArticle[];
  };
}

// --- backend Phase 3: GET /api/evaluation/runs, GET /api/evaluation/runs/{id}, GET /api/analytics/storage
export interface EvaluationRunSummary {
  runId: string;
  startedAt: string;
  dataset: string;
  embeddingModel: string;
  embeddingDimensions: number;
  queryCount: number;
  k: number[];
}

export interface EvaluationSummaryRow {
  engine: SearchEngine;
  mode: SearchMode;
  hybridStrategy?: HybridStrategy;
  bm25Weight?: number;
  vectorWeight?: number;
  k: number;
  precisionAtK: number;
  recallAtK: number;
  mrr: number;
  ndcgAtK: number;
  latencyMeanMs: number;
  latencyP50Ms: number;
  latencyP95Ms: number;
}

export interface EvaluationRun extends EvaluationRunSummary {
  rows: EvaluationSummaryRow[];
}

export interface EngineStorageStats {
  engine: SearchEngine;
  documentCount: number;
  storageBytes: number;
  indexingElapsedMs?: number;
}

// --- errors, normalised by httpClient
export interface ApiError {
  status: number; // 400 validation, 502 engine error, 503 engine unreachable; 0 when the API itself did not answer
  title: string;
  detail?: string;
}
