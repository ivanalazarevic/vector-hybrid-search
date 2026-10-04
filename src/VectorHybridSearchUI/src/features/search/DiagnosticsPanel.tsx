import type { SearchDiagnostics } from "../../api";
import styles from "./DiagnosticsPanel.module.css";

interface DiagnosticsPanelProps {
  queryId: string;
  diagnostics: SearchDiagnostics;
}

/** Collapsed by default: what the engine was asked to do, for checking a result against the thesis text. */
export function DiagnosticsPanel({ queryId, diagnostics }: DiagnosticsPanelProps) {
  const entries: [string, string][] = [
    ["Query id", queryId],
    ["Strategy", diagnostics.strategy],
    ["Requested top K", String(diagnostics.requestedTopK)],
    ...Object.entries(diagnostics.metadata),
  ];

  return (
    <details className={styles.panel}>
      <summary className={styles.summary}>Diagnostics</summary>
      <dl className={styles.list}>
        {entries.map(([name, value]) => (
          <div key={name} className={styles.entry}>
            <dt className={styles.name}>{name}</dt>
            <dd className={styles.value}>{value}</dd>
          </div>
        ))}
      </dl>
    </details>
  );
}
