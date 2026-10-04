import { useMemo, useState, type ReactNode } from "react";
import type { EvaluationSummaryRow } from "../../api";
import controls from "../../components/controls.module.css";
import { EngineBadge } from "../../components/EngineBadge";
import { copyText } from "../../lib/copyText";
import { formatMetric } from "../../lib/format";
import { ENGINE_LABEL, MODE_LABEL } from "../../lib/labels";
import { MODES } from "../../lib/queryState";
import { configurationLabel, fusionLabel } from "./configuration";
import { toCsv } from "./csv";
import styles from "./MetricTable.module.css";

type MetricKey = "precisionAtK" | "recallAtK" | "mrr" | "ndcgAtK" | "latencyP50Ms" | "latencyP95Ms";
type SortKey = "engine" | "configuration" | MetricKey;

interface Sort {
  key: SortKey;
  descending: boolean;
}

interface MetricColumn {
  key: MetricKey;
  label: (k: number) => string;
  /** Which end of the column is the good one. */
  best: "highest" | "lowest";
  format: (value: number) => string;
}

const latency = (value: number) => value.toFixed(1);

const METRIC_COLUMNS: readonly MetricColumn[] = [
  { key: "precisionAtK", label: (k) => `P@${k}`, best: "highest", format: formatMetric },
  { key: "recallAtK", label: (k) => `R@${k}`, best: "highest", format: formatMetric },
  { key: "mrr", label: () => "MRR", best: "highest", format: formatMetric },
  { key: "ndcgAtK", label: (k) => `nDCG@${k}`, best: "highest", format: formatMetric },
  { key: "latencyP50Ms", label: () => "p50 ms", best: "lowest", format: latency },
  { key: "latencyP95Ms", label: () => "p95 ms", best: "lowest", format: latency },
];

function compare(left: EvaluationSummaryRow, right: EvaluationSummaryRow, key: SortKey): number {
  switch (key) {
    case "engine":
      return ENGINE_LABEL[left.engine].localeCompare(ENGINE_LABEL[right.engine]);
    case "configuration":
      return (
        MODES.indexOf(left.mode) - MODES.indexOf(right.mode) ||
        configurationLabel(left).localeCompare(configurationLabel(right))
      );
    default:
      return left[key] - right[key];
  }
}

function bestValues(rows: readonly EvaluationSummaryRow[]): Record<MetricKey, number | undefined> {
  const best = {} as Record<MetricKey, number | undefined>;
  for (const column of METRIC_COLUMNS) {
    const values = rows.map((row) => row[column.key]);
    best[column.key] =
      values.length < 2 ? undefined : column.best === "highest" ? Math.max(...values) : Math.min(...values);
  }
  return best;
}

interface MetricTableProps {
  /** Rows of one run at one K. */
  rows: readonly EvaluationSummaryRow[];
  k: number;
  /** Shown left of the copy button, e.g. the K selector. */
  toolbar?: ReactNode;
}

export function MetricTable({ rows, k, toolbar }: MetricTableProps) {
  // No sort = the order the run reports, which groups rows by engine.
  const [sort, setSort] = useState<Sort | undefined>(undefined);
  const [copyStatus, setCopyStatus] = useState<"idle" | "copied" | "failed">("idle");

  const sorted = useMemo(() => {
    if (!sort) {
      return rows;
    }
    const direction = sort.descending ? -1 : 1;
    return [...rows].sort((left, right) => direction * compare(left, right, sort.key));
  }, [rows, sort]);
  const best = useMemo(() => bestValues(rows), [rows]);

  const toggleSort = (key: SortKey, descendingFirst: boolean) =>
    setSort((current) =>
      current?.key === key ? { key, descending: !current.descending } : { key, descending: descendingFirst },
    );

  const copyCsv = async () => {
    setCopyStatus((await copyText(toCsv(sorted))) ? "copied" : "failed");
    setTimeout(() => setCopyStatus("idle"), 2500);
  };

  const header = (key: SortKey, label: string, numeric: boolean, descendingFirst: boolean) => {
    const active = sort?.key === key;
    return (
      <th
        key={key}
        scope="col"
        className={numeric ? styles.numberHead : styles.textHead}
        aria-sort={active ? (sort?.descending ? "descending" : "ascending") : "none"}
      >
        <button type="button" className={styles.sort} onClick={() => toggleSort(key, descendingFirst)}>
          {label}
          <span className={styles.arrow} aria-hidden="true">
            {active ? (sort?.descending ? "▼" : "▲") : ""}
          </span>
        </button>
      </th>
    );
  };

  return (
    <div className={styles.block}>
      <div className={styles.toolbar}>
        {toolbar}
        <div className={styles.copy}>
          <span className={styles.copyStatus} role="status">
            {copyStatus === "copied" && `Copied ${sorted.length} rows`}
            {copyStatus === "failed" && "The browser blocked the clipboard"}
          </span>
          <button type="button" className={controls.buttonQuiet} onClick={() => void copyCsv()}>
            Copy as CSV
          </button>
        </div>
      </div>

      <div className={styles.scroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              {header("engine", "Engine", false, false)}
              {header("configuration", "Mode", false, false)}
              <th scope="col" className={styles.textHead}>
                Fusion (BM25 : vector)
              </th>
              {METRIC_COLUMNS.map((column) => header(column.key, column.label(k), true, column.best === "highest"))}
            </tr>
          </thead>
          <tbody>
            {sorted.map((row) => (
              <tr key={`${row.engine}:${configurationLabel(row)}`}>
                <th scope="row" className={styles.engine}>
                  <EngineBadge engine={row.engine} />
                </th>
                <td>{MODE_LABEL[row.mode]}</td>
                <td>{fusionLabel(row)}</td>
                {METRIC_COLUMNS.map((column) => (
                  <td key={column.key} className={row[column.key] === best[column.key] ? styles.best : styles.number}>
                    {column.format(row[column.key])}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className={styles.note}>
        Bold marks the best value in each column: highest for the retrieval metrics, lowest for latency.
      </p>
    </div>
  );
}
