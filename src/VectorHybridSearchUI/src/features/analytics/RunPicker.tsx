import type { EvaluationRunSummary } from "../../api";
import { formatDateTime, formatInteger } from "../../lib/format";
import styles from "./RunPicker.module.css";

interface RunPickerProps {
  runs: readonly EvaluationRunSummary[];
  selectedRunId: string | undefined;
  onSelect: (runId: string) => void;
}

export function RunPicker({ runs, selectedRunId, onSelect }: RunPickerProps) {
  return (
    <fieldset className={styles.picker}>
      <legend className={styles.legend}>Evaluation run</legend>
      <div className={styles.table}>
        <div className={styles.head} aria-hidden="true">
          <span />
          <span>Started (UTC)</span>
          <span>Dataset</span>
          <span>Embedding model</span>
          <span className={styles.number}>Queries</span>
          <span>K</span>
        </div>
        {runs.map((run) => (
          <label key={run.runId} className={run.runId === selectedRunId ? styles.rowSelected : styles.row}>
            <input
              className={styles.radio}
              type="radio"
              name="evaluation-run"
              checked={run.runId === selectedRunId}
              onChange={() => onSelect(run.runId)}
            />
            <span className={styles.started}>{formatDateTime(run.startedAt)}</span>
            <span>{run.dataset}</span>
            <span>
              {run.embeddingModel}
              <span className={styles.dimensions}> ({run.embeddingDimensions} dimensions)</span>
            </span>
            <span className={styles.number}>
              {formatInteger(run.queryCount)}
              <span className="visually-hidden"> queries</span>
            </span>
            <span>
              <span className="visually-hidden">K </span>
              {run.k.join(", ")}
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
