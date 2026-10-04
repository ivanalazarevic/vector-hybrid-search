import type { HybridStrategy, SearchEngine, SearchMode } from "../api";

export const ENGINE_LABEL: Record<SearchEngine, string> = {
  Elasticsearch: "Elasticsearch",
  MongoDbAtlas: "MongoDB Atlas",
};

export const MODE_LABEL: Record<SearchMode, string> = {
  Bm25: "BM25",
  Vector: "Vector",
  Hybrid: "Hybrid",
};

export const STRATEGY_LABEL: Record<HybridStrategy, string> = {
  Rrf: "RRF",
  Native: "Native",
};
