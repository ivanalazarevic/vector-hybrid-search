import type {
  ApiError,
  EvaluationRun,
  EvaluationRunSummary,
  EvaluationSummaryRow,
  HybridStrategy,
  SearchEngine,
  SearchMode,
} from "../types";

// One configuration = engine x mode x hybrid settings. `quality` is its nDCG@10; the other
// metrics and the other K values are derived from it so a run stays internally consistent.
interface Configuration {
  engine: SearchEngine;
  mode: SearchMode;
  hybridStrategy?: HybridStrategy;
  bm25Weight?: number;
  vectorWeight?: number;
  quality: number;
  p50: number;
  p95: number;
}

const rrf = { hybridStrategy: "Rrf", bm25Weight: 1, vectorWeight: 1 } as const;
const native = { hybridStrategy: "Native", bm25Weight: 0.7, vectorWeight: 0.3 } as const;

function round(value: number, digits: number): number {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

function buildRows(configurations: readonly Configuration[], ks: readonly number[]): EvaluationSummaryRow[] {
  return configurations.flatMap(({ quality, p50, p95, ...configuration }) =>
    ks.map((k) => {
      const depth = Math.log2(k / 10);
      return {
        ...configuration,
        k,
        precisionAtK: round(quality * 0.62 * (10 / k) ** 0.35, 3),
        recallAtK: round(Math.min(0.99, quality * 0.92 * (k / 10) ** 0.3), 3),
        mrr: round(Math.min(0.99, quality * 1.09), 3),
        ndcgAtK: round(quality * (1 + 0.04 * depth), 3),
        latencyMeanMs: round(p50 * 1.16 * (1 + 0.03 * depth), 1),
        latencyP50Ms: round(p50 * (1 + 0.03 * depth), 1),
        latencyP95Ms: round(p95 * (1 + 0.04 * depth), 1),
      };
    }),
  );
}

// Run 1: a real embedding model. Hybrid beats both single modes.
const semanticRun: EvaluationRun = {
  runId: "run-2026-09-30-minilm",
  startedAt: "2026-09-30T09:42:00Z",
  dataset: "BBC News (test split, 1,000 articles)",
  embeddingModel: "all-MiniLM-L6-v2",
  embeddingDimensions: 384,
  queryCount: 50,
  k: [5, 10, 20],
  rows: buildRows(
    [
      { engine: "Elasticsearch", mode: "Bm25", quality: 0.612, p50: 9.4, p95: 21.8 },
      { engine: "Elasticsearch", mode: "Vector", quality: 0.655, p50: 31.2, p95: 58.6 },
      { engine: "Elasticsearch", mode: "Hybrid", ...rrf, quality: 0.731, p50: 38.9, p95: 71.4 },
      { engine: "Elasticsearch", mode: "Hybrid", ...native, quality: 0.702, p50: 36.1, p95: 66.9 },
      { engine: "MongoDbAtlas", mode: "Bm25", quality: 0.598, p50: 14.2, p95: 38.5 },
      { engine: "MongoDbAtlas", mode: "Vector", quality: 0.651, p50: 36.7, p95: 74.3 },
      { engine: "MongoDbAtlas", mode: "Hybrid", ...rrf, quality: 0.722, p50: 49.5, p95: 96.2 },
      { engine: "MongoDbAtlas", mode: "Hybrid", ...native, quality: 0.688, p50: 47.8, p95: 91.0 },
    ],
    [5, 10, 20],
  ),
};

// Run 2: the placeholder embeddings the backend uses today. Vector metrics are near zero and
// drag hybrid below plain BM25.
const placeholderRun: EvaluationRun = {
  runId: "run-2026-09-24-placeholder",
  startedAt: "2026-09-24T16:05:00Z",
  dataset: "BBC News (test split, 1,000 articles)",
  embeddingModel: "sha256-placeholder",
  embeddingDimensions: 384,
  queryCount: 50,
  k: [5, 10, 20],
  rows: buildRows(
    [
      { engine: "Elasticsearch", mode: "Bm25", quality: 0.612, p50: 9.1, p95: 20.9 },
      { engine: "Elasticsearch", mode: "Vector", quality: 0.012, p50: 12.3, p95: 24.0 },
      { engine: "Elasticsearch", mode: "Hybrid", ...rrf, quality: 0.447, p50: 19.6, p95: 37.2 },
      { engine: "Elasticsearch", mode: "Hybrid", ...native, quality: 0.571, p50: 17.9, p95: 33.5 },
      { engine: "MongoDbAtlas", mode: "Bm25", quality: 0.598, p50: 13.8, p95: 37.1 },
      { engine: "MongoDbAtlas", mode: "Vector", quality: 0.009, p50: 17.4, p95: 41.6 },
      { engine: "MongoDbAtlas", mode: "Hybrid", ...rrf, quality: 0.431, p50: 29.3, p95: 61.8 },
      { engine: "MongoDbAtlas", mode: "Hybrid", ...native, quality: 0.556, p50: 28.0, p95: 58.4 },
    ],
    [5, 10, 20],
  ),
};

// Run 3: an early partial run with a single K and no hybrid rows.
const earlyRun: EvaluationRun = {
  runId: "run-2026-09-12-bm25-baseline",
  startedAt: "2026-09-12T11:20:00Z",
  dataset: "BBC News (test split, 200 articles)",
  embeddingModel: "sha256-placeholder",
  embeddingDimensions: 384,
  queryCount: 20,
  k: [10],
  rows: buildRows(
    [
      { engine: "Elasticsearch", mode: "Bm25", quality: 0.664, p50: 6.2, p95: 13.7 },
      { engine: "Elasticsearch", mode: "Vector", quality: 0.031, p50: 9.8, p95: 18.1 },
      { engine: "MongoDbAtlas", mode: "Bm25", quality: 0.649, p50: 11.5, p95: 29.4 },
    ],
    [10],
  ),
};

const RUNS: readonly EvaluationRun[] = [semanticRun, placeholderRun, earlyRun];

export function mockListRuns(): EvaluationRunSummary[] {
  return RUNS.map(({ rows: _rows, ...summary }) => summary);
}

export function mockGetRun(runId: string): EvaluationRun {
  const run = RUNS.find((candidate) => candidate.runId === runId);
  if (!run) {
    const error: ApiError = { status: 404, title: `Evaluation run '${runId}' was not found.` };
    throw error;
  }
  return run;
}
