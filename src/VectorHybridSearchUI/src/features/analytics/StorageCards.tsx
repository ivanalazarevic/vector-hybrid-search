import type { EngineStorageStats } from "../../api";
import { EngineBadge } from "../../components/EngineBadge";
import { formatBytes, formatDuration, formatInteger } from "../../lib/format";
import styles from "./StorageCards.module.css";

export function StorageCards({ stats }: { stats: readonly EngineStorageStats[] }) {
  return (
    <div className={styles.cards}>
      {stats.map((engine) => (
        <section key={engine.engine} className={styles.card} data-engine={engine.engine}>
          <h3 className={styles.name}>
            <EngineBadge engine={engine.engine} size="lg" />
          </h3>
          <dl className={styles.stats}>
            <div>
              <dt className={styles.statName}>Index size</dt>
              <dd className={styles.statValue}>{formatBytes(engine.storageBytes)}</dd>
            </div>
            <div>
              <dt className={styles.statName}>Indexing time</dt>
              <dd className={styles.statValue}>
                {engine.indexingElapsedMs === undefined ? "not measured" : formatDuration(engine.indexingElapsedMs)}
              </dd>
            </div>
            <div>
              <dt className={styles.statName}>Documents (chunks)</dt>
              <dd className={styles.statValue}>{formatInteger(engine.documentCount)}</dd>
            </div>
          </dl>
        </section>
      ))}
    </div>
  );
}
