import { describe, expect, it } from "vitest";
import type { CompareRequest, SearchRequest } from "../types";
import { mockCompare } from "./compare";
import { mockGetRun, mockListRuns } from "./evaluation";
import { mockSearch } from "./search";

// These pin the awkward cases the plan requires the mock data to contain, so a later edit
// to the corpus cannot silently remove one.

const searchRequest = (overrides: Partial<SearchRequest>): SearchRequest => ({
  query: "interest rates",
  engine: "Elasticsearch",
  mode: "Bm25",
  topK: 10,
  includeDiagnostics: true,
  ...overrides,
});

const compareRequest = (query: string): CompareRequest => ({ query, mode: "Bm25", topK: 10, includeDiagnostics: false });

function thrownBy(run: () => unknown): unknown {
  try {
    run();
  } catch (error) {
    return error;
  }
  return undefined;
}

describe("mock search", () => {
  it("returns zero results for a query nothing matches", () => {
    expect(mockSearch(searchRequest({ query: "xylophone" })).results).toEqual([]);
  });

  it("includes a result with no category and no source", () => {
    const { results } = mockSearch(searchRequest({}));

    expect(results.some((result) => result.category === undefined && result.source === undefined)).toBe(true);
  });

  it("includes a very long title", () => {
    const { results } = mockSearch(searchRequest({ query: "election spending" }));

    expect(Math.max(...results.map((result) => result.title.length))).toBeGreaterThan(150);
  });

  it("returns snippets with several highlights", () => {
    const { results } = mockSearch(searchRequest({}));

    expect(results.some((result) => (result.snippet.match(/<em>/g) ?? []).length >= 3)).toBe(true);
  });

  it("returns a snippet with escaped & and < characters", () => {
    const { results } = mockSearch(searchRequest({ query: "R&D research" }));

    expect(results[0].snippet).toContain("&amp;");
    expect(results[0].snippet).toContain("&lt;15%");
  });

  it("returns hybrid results that have only a BM25 rank or only a vector rank", () => {
    const { results } = mockSearch(searchRequest({ mode: "Hybrid", topK: 5 }));

    expect(results.some((result) => result.bm25Rank !== undefined && result.vectorRank === undefined)).toBe(true);
    expect(results.some((result) => result.bm25Rank === undefined && result.vectorRank !== undefined)).toBe(true);
  });

  it("ranks from 1 without gaps and reports timings only when an embedding was needed", () => {
    const bm25 = mockSearch(searchRequest({}));
    const vector = mockSearch(searchRequest({ mode: "Vector" }));

    expect(bm25.results.map((result) => result.rank)).toEqual(bm25.results.map((_, index) => index + 1));
    expect(bm25.timings).toBeUndefined();
    expect(vector.timings).toBeDefined();
  });

  it.each([400, 502, 503])("throws an ApiError for #%i", (status) => {
    expect(thrownBy(() => mockSearch(searchRequest({ query: `interest rates #${status}` })))).toMatchObject({ status });
  });

  it("rejects an empty query and an out-of-range top K with 400", () => {
    expect(thrownBy(() => mockSearch(searchRequest({ query: "  " })))).toMatchObject({ status: 400 });
    expect(thrownBy(() => mockSearch(searchRequest({ topK: 101 })))).toMatchObject({ status: 400 });
  });
});

describe("mock compare", () => {
  it("overlaps partially for an ordinary query", () => {
    const { overlap } = mockCompare(compareRequest("interest rates"));

    expect(overlap.sharedCount).toBeGreaterThan(0);
    expect(overlap.jaccard).toBeGreaterThan(0);
    expect(overlap.jaccard).toBeLessThan(1);
  });

  it("reports one engine failing while the other answers", () => {
    const { engines, overlap } = mockCompare(compareRequest("interest rates #mongo-down"));
    const [elasticsearch, mongoDb] = engines;

    expect(elasticsearch.response?.results.length).toBeGreaterThan(0);
    expect(elasticsearch.error).toBeNull();
    expect(mongoDb.response).toBeNull();
    expect(mongoDb.error).toEqual(expect.any(String));
    expect(overlap.sharedCount).toBe(0);
  });

  it("has a case with no overlap", () => {
    const { engines, overlap } = mockCompare(compareRequest("election #no-overlap"));

    expect(engines.every((entry) => (entry.response?.results.length ?? 0) > 0)).toBe(true);
    expect(overlap).toEqual({ sharedCount: 0, jaccard: 0, shared: [] });
  });

  it("has a case with full overlap in a different order", () => {
    const { engines, overlap } = mockCompare(compareRequest("election #full-overlap"));
    const [elasticsearch, mongoDb] = engines.map((entry) => entry.response!.results.map((result) => result.articleId));

    expect(overlap.jaccard).toBe(1);
    expect([...mongoDb].sort()).toEqual([...elasticsearch].sort());
    expect(mongoDb).not.toEqual(elasticsearch);
  });
});

describe("mock evaluation", () => {
  it("lists runs without their rows", () => {
    const runs = mockListRuns();

    expect(runs.length).toBeGreaterThan(1);
    expect(runs.every((run) => !("rows" in run))).toBe(true);
  });

  it("has a run whose vector metrics are near zero", () => {
    const placeholder = mockListRuns().find((run) => run.embeddingModel === "sha256-placeholder")!;
    const vectorRows = mockGetRun(placeholder.runId).rows.filter((row) => row.mode === "Vector");

    expect(vectorRows.length).toBeGreaterThan(0);
    expect(vectorRows.every((row) => row.ndcgAtK < 0.05 && row.mrr < 0.05)).toBe(true);
  });

  it("has a row for every K the run lists", () => {
    for (const summary of mockListRuns()) {
      const run = mockGetRun(summary.runId);
      expect([...new Set(run.rows.map((row) => row.k))].sort((a, b) => a - b)).toEqual(run.k);
    }
  });
});
