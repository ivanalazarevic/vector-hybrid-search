import type { SearchTimings } from "../api";
import { formatMs } from "../lib/format";
import styles from "./LatencyBadge.module.css";

interface LatencyBadgeProps {
  elapsedMs: number;
  timings?: SearchTimings;
}

/** Total latency, with the embedding / engine split when the backend reports it. */
export function LatencyBadge({ elapsedMs, timings }: LatencyBadgeProps) {
  return (
    <span className={styles.latency}>
      <span className={styles.total}>{formatMs(elapsedMs)}</span>
      <span className={styles.split}>
        {timings ? `embedding ${formatMs(timings.embeddingMs)}, engine ${formatMs(timings.engineMs)}` : "total"}
      </span>
    </span>
  );
}
