const integer = new Intl.NumberFormat("en-GB");

const dateTime = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  timeZone: "UTC",
});

export function formatInteger(value: number): string {
  return integer.format(value);
}

/** Latency: one decimal below 100 ms, whole milliseconds above. */
export function formatMs(ms: number): string {
  return `${ms < 100 ? ms.toFixed(1) : Math.round(ms)} ms`;
}

/** Longer durations such as indexing time. */
export function formatDuration(ms: number): string {
  if (ms < 1000) {
    return `${Math.round(ms)} ms`;
  }
  const seconds = ms / 1000;
  if (seconds < 60) {
    return `${seconds.toFixed(1)} s`;
  }
  return `${Math.floor(seconds / 60)} min ${Math.round(seconds % 60)} s`;
}

/** Scores span very different ranges (BM25 ~10, cosine ~0.5, RRF ~0.03), so precision follows magnitude. */
export function formatScore(score: number): string {
  const magnitude = Math.abs(score);
  if (magnitude >= 100) {
    return score.toFixed(1);
  }
  if (magnitude >= 1) {
    return score.toFixed(2);
  }
  return score.toFixed(4);
}

/** Retrieval metrics in [0, 1], three decimals as in IR papers. */
export function formatMetric(value: number): string {
  return value.toFixed(3);
}

export function formatBytes(bytes: number): string {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${unit === 0 ? value : value.toFixed(1)} ${units[unit]}`;
}

/** "30 Sep 2026, 09:42" in UTC, so screenshots do not depend on the machine's time zone. */
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : dateTime.format(date);
}
