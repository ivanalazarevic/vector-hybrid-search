import { describe, expect, it } from "vitest";
import type { EvaluationSummaryRow } from "../../api";
import { toCsv } from "./csv";

const row: EvaluationSummaryRow = {
  engine: "Elasticsearch",
  mode: "Hybrid",
  hybridStrategy: "Rrf",
  bm25Weight: 1,
  vectorWeight: 0.5,
  k: 10,
  precisionAtK: 0.453,
  recallAtK: 0.672,
  mrr: 0.797,
  ndcgAtK: 0.731,
  latencyMeanMs: 45.1,
  latencyP50Ms: 38.9,
  latencyP95Ms: 71.4,
};

describe("toCsv", () => {
  it("writes a header line and one line per row", () => {
    expect(toCsv([row])).toBe(
      [
        "engine,mode,hybrid_strategy,bm25_weight,vector_weight,k,precision_at_k,recall_at_k,mrr,ndcg_at_k,latency_mean_ms,latency_p50_ms,latency_p95_ms",
        "Elasticsearch,Hybrid,Rrf,1,0.5,10,0.453,0.672,0.797,0.731,45.1,38.9,71.4",
      ].join("\n"),
    );
  });

  it("leaves hybrid columns empty for non-hybrid rows", () => {
    const bm25: EvaluationSummaryRow = {
      ...row,
      mode: "Bm25",
      hybridStrategy: undefined,
      bm25Weight: undefined,
      vectorWeight: undefined,
    };

    expect(toCsv([bm25]).split("\n")[1]).toBe("Elasticsearch,Bm25,,,,10,0.453,0.672,0.797,0.731,45.1,38.9,71.4");
  });

  it("writes only the header for no rows", () => {
    expect(toCsv([]).split("\n")).toHaveLength(1);
  });
});
