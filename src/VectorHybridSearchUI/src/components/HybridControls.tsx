import type { HybridStrategy } from "../api";
import { STRATEGY_LABEL } from "../lib/labels";
import { STRATEGIES } from "../lib/queryState";
import controls from "./controls.module.css";
import styles from "./HybridControls.module.css";
import { Segmented } from "./Segmented";

const OPTIONS = STRATEGIES.map((strategy) => ({ value: strategy, label: STRATEGY_LABEL[strategy] }));

/** Numeric fields are kept as text while editing, so a half-typed value is not rewritten under the cursor. */
export interface HybridDraft {
  strategy: HybridStrategy;
  bm25Weight: string;
  vectorWeight: string;
  rrfK: string;
}

export type HybridNumberField = "bm25Weight" | "vectorWeight" | "rrfK";

interface HybridControlsProps {
  value: HybridDraft;
  onStrategyChange: (strategy: HybridStrategy) => void;
  onNumberChange: (field: HybridNumberField, value: string) => void;
}

export function HybridControls({ value, onStrategyChange, onNumberChange }: HybridControlsProps) {
  return (
    <div className={styles.row}>
      <Segmented legend="Fusion" name="strategy" value={value.strategy} options={OPTIONS} onChange={onStrategyChange} />
      <label className={controls.field}>
        <span className={controls.label}>BM25 weight</span>
        <input
          className={`${controls.input} ${styles.number}`}
          type="number"
          min={0}
          max={10}
          step={0.1}
          value={value.bm25Weight}
          onChange={(event) => onNumberChange("bm25Weight", event.target.value)}
        />
      </label>
      <label className={controls.field}>
        <span className={controls.label}>Vector weight</span>
        <input
          className={`${controls.input} ${styles.number}`}
          type="number"
          min={0}
          max={10}
          step={0.1}
          value={value.vectorWeight}
          onChange={(event) => onNumberChange("vectorWeight", event.target.value)}
        />
      </label>
      {value.strategy === "Rrf" && (
        <label className={controls.field}>
          <span className={controls.label}>RRF constant k</span>
          <input
            className={`${controls.input} ${styles.number}`}
            type="number"
            min={1}
            max={1000}
            step={1}
            value={value.rrfK}
            onChange={(event) => onNumberChange("rrfK", event.target.value)}
          />
        </label>
      )}
      <p className={styles.note}>
        {value.strategy === "Rrf"
          ? "RRF merges the BM25 and vector rankings by rank position."
          : "Native uses the engine's own score combination."}
      </p>
    </div>
  );
}
