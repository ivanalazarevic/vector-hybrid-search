import type { EvaluationSummaryRow } from "../../api";

const COLUMNS: [header: string, value: (row: EvaluationSummaryRow) => string | number | undefined][] = [
  ["engine", (row) => row.engine],
  ["mode", (row) => row.mode],
  ["hybrid_strategy", (row) => row.hybridStrategy],
  ["bm25_weight", (row) => row.bm25Weight],
  ["vector_weight", (row) => row.vectorWeight],
  ["k", (row) => row.k],
  ["precision_at_k", (row) => row.precisionAtK],
  ["recall_at_k", (row) => row.recallAtK],
  ["mrr", (row) => row.mrr],
  ["ndcg_at_k", (row) => row.ndcgAtK],
  ["latency_mean_ms", (row) => row.latencyMeanMs],
  ["latency_p50_ms", (row) => row.latencyP50Ms],
  ["latency_p95_ms", (row) => row.latencyP95Ms],
];

function cell(value: string | number | undefined): string {
  if (value === undefined) {
    return "";
  }
  const text = String(value);
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** The metric rows as CSV with a header line, full precision, in the order given. */
export function toCsv(rows: readonly EvaluationSummaryRow[]): string {
  const lines = [COLUMNS.map(([header]) => header).join(",")];
  for (const row of rows) {
    lines.push(COLUMNS.map(([, value]) => cell(value(row))).join(","));
  }
  return lines.join("\n");
}
