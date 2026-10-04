import { describe, expect, it } from "vitest";
import { describeDelta, rankDiff } from "./rankDiff";

const ranked = (...articleIds: string[]) => articleIds.map((articleId, index) => ({ articleId, rank: index + 1 }));

describe("rankDiff", () => {
  it("marks articles the other engine did not return as unique", () => {
    const diffs = rankDiff(ranked("a", "b"), ranked("b", "c"));

    expect(diffs.get("a")).toEqual({ status: "unique" });
  });

  it("gives a positive delta when this engine ranks the article higher", () => {
    const diffs = rankDiff(ranked("a", "b", "c"), ranked("c", "b", "a"));

    expect(diffs.get("a")).toEqual({ status: "shared", otherRank: 3, delta: 2 });
    expect(diffs.get("b")).toEqual({ status: "shared", otherRank: 2, delta: 0 });
    expect(diffs.get("c")).toEqual({ status: "shared", otherRank: 1, delta: -2 });
  });

  it("is antisymmetric between the two engines", () => {
    const left = ranked("a", "b", "c");
    const right = ranked("b", "c", "a");

    const fromLeft = rankDiff(left, right).get("a");
    const fromRight = rankDiff(right, left).get("a");

    expect(fromLeft).toMatchObject({ delta: 2 });
    expect(fromRight).toMatchObject({ delta: -2 });
  });

  it("has one entry per own result and none for the other engine's extras", () => {
    const diffs = rankDiff(ranked("a"), ranked("a", "b", "c"));

    expect([...diffs.keys()]).toEqual(["a"]);
  });

  it("handles empty lists", () => {
    expect(rankDiff([], ranked("a")).size).toBe(0);
    expect(rankDiff(ranked("a"), []).get("a")).toEqual({ status: "unique" });
  });
});

describe("describeDelta", () => {
  it("words the difference from this engine's side", () => {
    expect(describeDelta(2)).toBe("2 higher");
    expect(describeDelta(-1)).toBe("1 lower");
    expect(describeDelta(0)).toBe("same rank");
  });
});
