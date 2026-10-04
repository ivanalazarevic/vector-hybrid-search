import type { ApiError, SearchEngine } from "../types";

// Mock-only switches written into the query text, e.g. "interest rates #503". They select a
// prepared failure or overlap case and are stripped before the query is matched.
//   #400 #502 #503            whole request fails with that status
//   #es-down #mongo-down      compare: that engine fails, the other still answers
//   #no-overlap               compare: the engines return disjoint articles
//   #full-overlap             compare: the engines return the same articles in a different order

const DIRECTIVE = /#([a-z0-9-]+)/gi;

export interface ParsedQuery {
  text: string;
  directives: ReadonlySet<string>;
}

export function parseDirectives(query: string): ParsedQuery {
  const directives = new Set<string>();
  const text = query
    .replace(DIRECTIVE, (_, name: string) => {
      directives.add(name.toLowerCase());
      return " ";
    })
    .replace(/\s+/g, " ")
    .trim();
  return { text, directives };
}

export function engineError(engine: SearchEngine, status: 502 | 503): ApiError {
  const elasticsearch = engine === "Elasticsearch";
  if (status === 503) {
    return {
      status: 503,
      title: "Search engine unavailable",
      detail: elasticsearch
        ? "No connection could be made because the target machine actively refused it. (localhost:9201)"
        : "A timeout occurred after 30000ms selecting a server. (localhost:27018)",
    };
  }
  return {
    status: 502,
    title: "Search engine error",
    detail: elasticsearch
      ? 'Elasticsearch search failed with status 400: {"error":{"type":"search_phase_execution_exception","reason":"all shards failed"}}'
      : "MongoDB search failed: PlanExecutor error during aggregation :: caused by :: index 'articles_search' is not ready (status: PENDING)",
  };
}

/** Throws the ApiError requested by a #400 / #502 / #503 directive, if any. */
export function throwForErrorDirective(directives: ReadonlySet<string>, engine: SearchEngine): void {
  if (directives.has("400")) {
    const error: ApiError = { status: 400, title: "Filters.PublishedFrom must be earlier than Filters.PublishedTo." };
    throw error;
  }
  if (directives.has("502")) {
    throw engineError(engine, 502);
  }
  if (directives.has("503")) {
    throw engineError(engine, 503);
  }
}
