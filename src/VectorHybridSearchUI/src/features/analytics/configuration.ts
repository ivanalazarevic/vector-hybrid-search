import type { EvaluationSummaryRow } from "../../api";
import { MODE_LABEL, STRATEGY_LABEL } from "../../lib/labels";

type Configuration = Pick<EvaluationSummaryRow, "mode" | "hybridStrategy" | "bm25Weight" | "vectorWeight">;

/** "RRF 1 : 1" (fusion, BM25 weight : vector weight) for hybrid rows, empty otherwise. */
export function fusionLabel(row: Configuration): string {
  if (row.mode !== "Hybrid" || !row.hybridStrategy) {
    return "";
  }
  const weights = row.bm25Weight !== undefined && row.vectorWeight !== undefined ? ` ${row.bm25Weight} : ${row.vectorWeight}` : "";
  return `${STRATEGY_LABEL[row.hybridStrategy]}${weights}`;
}

/** The engine-independent part of a configuration: "BM25", "Vector", "Hybrid RRF 1 : 1". */
export function configurationLabel(row: Configuration): string {
  const fusion = fusionLabel(row);
  return fusion === "" ? MODE_LABEL[row.mode] : `${MODE_LABEL[row.mode]} ${fusion}`;
}
