export interface Ranked {
  articleId: string;
  rank: number;
}

export type RankDiff =
  /** The other engine did not return this article. */
  | { status: "unique" }
  /** Both engines returned it. `delta` is positive when this engine ranks it higher (closer to 1) than the other. */
  | { status: "shared"; otherRank: number; delta: number };

/** For every result of one engine, how it relates to the other engine's result list. */
export function rankDiff(own: readonly Ranked[], other: readonly Ranked[]): Map<string, RankDiff> {
  const otherRanks = new Map(other.map((result) => [result.articleId, result.rank]));
  const diffs = new Map<string, RankDiff>();

  for (const result of own) {
    const otherRank = otherRanks.get(result.articleId);
    diffs.set(
      result.articleId,
      otherRank === undefined ? { status: "unique" } : { status: "shared", otherRank, delta: otherRank - result.rank },
    );
  }

  return diffs;
}

/** "2 higher", "1 lower" or "same rank", from this engine's point of view. */
export function describeDelta(delta: number): string {
  if (delta === 0) {
    return "same rank";
  }
  return `${Math.abs(delta)} ${delta > 0 ? "higher" : "lower"}`;
}
