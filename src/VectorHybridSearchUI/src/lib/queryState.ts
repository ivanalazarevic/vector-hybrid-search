import type {
  CompareRequest,
  HybridOptions,
  HybridStrategy,
  SearchEngine,
  SearchFilters,
  SearchMode,
  SearchRequest,
} from "../api";

/** Everything the query form holds. It round-trips through the URL so a search can be shared. */
export interface QueryState {
  query: string;
  engine: SearchEngine;
  mode: SearchMode;
  topK: number;
  category: string;
  source: string;
  hybrid: HybridOptions;
}

export const ENGINES: readonly SearchEngine[] = ["Elasticsearch", "MongoDbAtlas"];
export const MODES: readonly SearchMode[] = ["Bm25", "Vector", "Hybrid"];
export const STRATEGIES: readonly HybridStrategy[] = ["Rrf", "Native"];

export const DEFAULT_QUERY_STATE: QueryState = {
  query: "",
  engine: "Elasticsearch",
  mode: "Bm25",
  topK: 10,
  category: "",
  source: "",
  hybrid: { strategy: "Rrf", bm25Weight: 1, vectorWeight: 1, rrfK: 60 },
};

function oneOf<T extends string>(value: string | null, allowed: readonly T[], fallback: T): T {
  return allowed.includes(value as T) ? (value as T) : fallback;
}

function numberIn(value: string | null, min: number, max: number, fallback: number): number {
  if (value === null || value.trim() === "") {
    return fallback;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.min(max, Math.max(min, parsed)) : fallback;
}

export function parseQueryState(params: URLSearchParams): QueryState {
  const defaults = DEFAULT_QUERY_STATE;
  return {
    query: params.get("q") ?? "",
    engine: oneOf(params.get("engine"), ENGINES, defaults.engine),
    mode: oneOf(params.get("mode"), MODES, defaults.mode),
    topK: Math.round(numberIn(params.get("k"), 1, 100, defaults.topK)),
    category: params.get("category") ?? "",
    source: params.get("source") ?? "",
    hybrid: {
      strategy: oneOf(params.get("strategy"), STRATEGIES, defaults.hybrid.strategy),
      bm25Weight: numberIn(params.get("bm25"), 0, 10, defaults.hybrid.bm25Weight),
      vectorWeight: numberIn(params.get("vector"), 0, 10, defaults.hybrid.vectorWeight),
      rrfK: Math.round(numberIn(params.get("rrfk"), 1, 1000, defaults.hybrid.rrfK)),
    },
  };
}

/** Only values that differ from the defaults are written, so URLs stay short. */
export function toSearchParams(state: QueryState, options: { includeEngine: boolean }): URLSearchParams {
  const defaults = DEFAULT_QUERY_STATE;
  const params = new URLSearchParams();
  const query = state.query.trim();

  if (query !== "") {
    params.set("q", query);
  }
  if (options.includeEngine && state.engine !== defaults.engine) {
    params.set("engine", state.engine);
  }
  if (state.mode !== defaults.mode) {
    params.set("mode", state.mode);
  }
  if (state.topK !== defaults.topK) {
    params.set("k", String(state.topK));
  }
  if (state.category.trim() !== "") {
    params.set("category", state.category.trim());
  }
  if (state.source.trim() !== "") {
    params.set("source", state.source.trim());
  }
  if (state.mode === "Hybrid") {
    if (state.hybrid.strategy !== defaults.hybrid.strategy) {
      params.set("strategy", state.hybrid.strategy);
    }
    if (state.hybrid.bm25Weight !== defaults.hybrid.bm25Weight) {
      params.set("bm25", String(state.hybrid.bm25Weight));
    }
    if (state.hybrid.vectorWeight !== defaults.hybrid.vectorWeight) {
      params.set("vector", String(state.hybrid.vectorWeight));
    }
    if (state.hybrid.rrfK !== defaults.hybrid.rrfK) {
      params.set("rrfk", String(state.hybrid.rrfK));
    }
  }

  return params;
}

function toFilters(state: QueryState): SearchFilters | undefined {
  const category = state.category.trim();
  const source = state.source.trim();
  if (category === "" && source === "") {
    return undefined;
  }
  return { category: category || undefined, source: source || undefined };
}

export function toCompareRequest(state: QueryState): CompareRequest {
  return {
    query: state.query.trim(),
    mode: state.mode,
    topK: state.topK,
    filters: toFilters(state),
    hybrid: state.mode === "Hybrid" ? state.hybrid : undefined,
    includeDiagnostics: true,
  };
}

export function toSearchRequest(state: QueryState): SearchRequest {
  return { ...toCompareRequest(state), engine: state.engine };
}
