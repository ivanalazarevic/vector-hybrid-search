import type { SearchMode } from "../types";

/** A query that reaches one of the prepared mock cases, with what it demonstrates. */
export interface MockExample {
  query: string;
  mode?: SearchMode;
  shows: string;
}

export const SEARCH_EXAMPLES: readonly MockExample[] = [
  { query: "interest rates", shows: "several highlights; one result without category or source" },
  { query: "election spending", shows: "a very long title" },
  { query: "R&D research", shows: "a snippet containing & and <" },
  { query: "interest rates", mode: "Hybrid", shows: "articles with only a BM25 rank or only a vector rank" },
  { query: "xylophone", shows: "zero results" },
  { query: "interest rates #400", shows: "validation error" },
  { query: "interest rates #502", shows: "engine error" },
  { query: "interest rates #503", shows: "engine unreachable" },
];

export const COMPARE_EXAMPLES: readonly MockExample[] = [
  { query: "interest rates", shows: "partial overlap" },
  { query: "election #full-overlap", shows: "same articles in a different order" },
  { query: "election #no-overlap", shows: "no shared articles" },
  { query: "interest rates #mongo-down", shows: "one engine failing" },
  { query: "xylophone", shows: "zero results in both engines" },
  { query: "interest rates #503", shows: "whole request failing" },
];
