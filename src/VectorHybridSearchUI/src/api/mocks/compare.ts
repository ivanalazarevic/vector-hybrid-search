import { isApiError } from "../client";
import type {
  CompareRequest,
  EngineComparisonEntry,
  SearchComparisonResponse,
  SearchEngine,
  SearchResponse,
  SearchResult,
} from "../types";
import { engineError, parseDirectives, throwForErrorDirective } from "./directives";
import { mockSearch } from "./search";

function rerank(results: readonly SearchResult[]): SearchResult[] {
  return results.map((result, index) => ({ ...result, rank: index + 1 }));
}

/** Gives a reordered list plausible, strictly descending scores taken from the original list. */
function withScoresFrom(results: readonly SearchResult[], scores: readonly number[]): SearchResult[] {
  return rerank(results).map((result, index) => ({ ...result, score: scores[index] ?? result.score }));
}

function runEngine(request: CompareRequest, engine: SearchEngine, down: boolean): EngineComparisonEntry {
  if (down) {
    const error = engineError(engine, 503);
    return { engine, response: null, error: `${error.title}: ${error.detail}` };
  }
  try {
    return { engine, response: mockSearch({ ...request, engine }), error: null };
  } catch (cause) {
    if (isApiError(cause) && cause.status !== 400) {
      return { engine, response: null, error: cause.detail ? `${cause.title}: ${cause.detail}` : cause.title };
    }
    throw cause;
  }
}

function computeOverlap(
  elasticsearch: SearchResponse | null,
  mongoDb: SearchResponse | null,
): SearchComparisonResponse["overlap"] {
  const left = elasticsearch?.results ?? [];
  const right = mongoDb?.results ?? [];
  const rightRanks = new Map(right.map((result) => [result.articleId, result.rank]));

  const shared = left
    .filter((result) => rightRanks.has(result.articleId))
    .map((result) => ({
      articleId: result.articleId,
      elasticsearchRank: result.rank,
      mongoDbRank: rightRanks.get(result.articleId)!,
    }));

  const union = left.length + right.length - shared.length;
  return {
    sharedCount: shared.length,
    jaccard: union === 0 ? 0 : Math.round((shared.length / union) * 1000) / 1000,
    shared,
  };
}

export function mockCompare(request: CompareRequest): SearchComparisonResponse {
  const { directives } = parseDirectives(request.query);
  throwForErrorDirective(directives, "Elasticsearch");

  const elasticsearch = runEngine(request, "Elasticsearch", directives.has("es-down"));
  const mongoDb = runEngine(request, "MongoDbAtlas", directives.has("mongo-down"));

  if (elasticsearch.response && mongoDb.response) {
    const mongoScores = mongoDb.response.results.map((result) => result.score);

    if (directives.has("no-overlap")) {
      // Deal one ranked list out to the two engines alternately.
      const pool = mockSearch({ ...request, engine: "Elasticsearch", topK: Math.min(100, request.topK * 2) }).results;
      elasticsearch.response.results = rerank(pool.filter((_, index) => index % 2 === 0));
      mongoDb.response.results = withScoresFrom(pool.filter((_, index) => index % 2 === 1), mongoScores);
    } else if (directives.has("full-overlap")) {
      // Same articles, with neighbouring pairs swapped on the MongoDB side.
      const swapped = [...elasticsearch.response.results];
      for (let index = 0; index + 1 < swapped.length; index += 2) {
        [swapped[index], swapped[index + 1]] = [swapped[index + 1], swapped[index]];
      }
      mongoDb.response.results = withScoresFrom(swapped, mongoScores);
    }
  }

  return {
    queryId: (elasticsearch.response ?? mongoDb.response)?.queryId ?? "mock-compare",
    query: request.query,
    mode: request.mode,
    engines: [elasticsearch, mongoDb],
    overlap: computeOverlap(elasticsearch.response, mongoDb.response),
  };
}
