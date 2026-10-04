import { describe, expect, it } from "vitest";
import { DEFAULT_QUERY_STATE, parseQueryState, toSearchParams, toSearchRequest, type QueryState } from "./queryState";

describe("queryState", () => {
  it("parses an empty URL to the defaults", () => {
    expect(parseQueryState(new URLSearchParams())).toEqual(DEFAULT_QUERY_STATE);
  });

  it("writes only non-default values", () => {
    const state: QueryState = { ...DEFAULT_QUERY_STATE, query: " interest rates " };

    expect(toSearchParams(state, { includeEngine: true }).toString()).toBe("q=interest+rates");
  });

  it("round-trips a full hybrid query through the URL", () => {
    const state: QueryState = {
      query: "election #no-overlap",
      engine: "MongoDbAtlas",
      mode: "Hybrid",
      topK: 5,
      category: "politics",
      source: "BBC News",
      hybrid: { strategy: "Native", bm25Weight: 0.7, vectorWeight: 0.3, rrfK: 20 },
    };

    expect(parseQueryState(toSearchParams(state, { includeEngine: true }))).toEqual(state);
  });

  it("falls back to defaults for unknown enum values and clamps numbers", () => {
    const state = parseQueryState(new URLSearchParams("q=x&engine=Solr&mode=Fuzzy&k=5000&bm25=abc"));

    expect(state.engine).toBe("Elasticsearch");
    expect(state.mode).toBe("Bm25");
    expect(state.topK).toBe(100);
    expect(state.hybrid.bm25Weight).toBe(1);
  });

  it("sends hybrid options only in hybrid mode and filters only when set", () => {
    const bm25 = toSearchRequest({ ...DEFAULT_QUERY_STATE, query: "rates" });
    const hybrid = toSearchRequest({ ...DEFAULT_QUERY_STATE, query: "rates", mode: "Hybrid", category: "business" });

    expect(bm25.hybrid).toBeUndefined();
    expect(bm25.filters).toBeUndefined();
    expect(hybrid.hybrid).toEqual(DEFAULT_QUERY_STATE.hybrid);
    expect(hybrid.filters).toEqual({ category: "business", source: undefined });
  });
});
