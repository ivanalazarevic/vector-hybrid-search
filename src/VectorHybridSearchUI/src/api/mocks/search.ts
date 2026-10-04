import type {
  ApiError,
  HybridOptions,
  SearchEngine,
  SearchFilters,
  SearchRequest,
  SearchResponse,
  SearchResult,
} from "../types";
import { CORPUS, type MockArticle } from "./corpus";
import { parseDirectives, throwForErrorDirective } from "./directives";

const WORD = /[A-Za-z0-9]+/g;
const STOPWORDS = new Set(["a", "an", "and", "as", "at", "for", "in", "of", "on", "the", "to"]);
const FRAGMENT_SIZE = 220;
const DEFAULT_HYBRID: HybridOptions = { strategy: "Rrf", bm25Weight: 1, vectorWeight: 1, rrfK: 60 };

interface Scored {
  article: MockArticle;
  score: number;
}

/** FNV-1a mapped to [0, 1). Gives the mock stable "random" numbers per input. */
export function hash(text: string): number {
  let value = 0x811c9dc5;
  for (let index = 0; index < text.length; index++) {
    value ^= text.charCodeAt(index);
    value = Math.imul(value, 0x01000193);
  }
  return (value >>> 0) / 0x100000000;
}

function stem(word: string): string {
  const lower = word.toLowerCase();
  return lower.length > 3 && lower.endsWith("s") ? lower.slice(0, -1) : lower;
}

function queryTerms(text: string): string[] {
  const terms = (text.match(WORD) ?? []).map(stem).filter((term) => !STOPWORDS.has(term));
  return [...new Set(terms)];
}

function matches(word: string, terms: readonly string[]): boolean {
  const stemmed = stem(word);
  return terms.some((term) => stemmed === term || (term.length >= 4 && stemmed.startsWith(term)));
}

function countMatches(text: string, terms: readonly string[]): number {
  return (text.match(WORD) ?? []).filter((word) => matches(word, terms)).length;
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** One fragment around the first match, HTML-escaped, with matching words wrapped in <em>. */
function buildSnippet(text: string, terms: readonly string[]): string {
  let firstMatch = -1;
  for (const word of text.matchAll(WORD)) {
    if (matches(word[0], terms)) {
      firstMatch = word.index ?? 0;
      break;
    }
  }

  const start = firstMatch > 80 ? text.lastIndexOf(" ", firstMatch - 60) + 1 : 0;
  const end = start + FRAGMENT_SIZE < text.length ? text.lastIndexOf(" ", start + FRAGMENT_SIZE) : text.length;
  const fragment = text.slice(start, end);

  let html = "";
  let cursor = 0;
  for (const word of fragment.matchAll(WORD)) {
    const index = word.index ?? 0;
    html += escapeHtml(fragment.slice(cursor, index));
    html += matches(word[0], terms) ? `<em>${word[0]}</em>` : word[0];
    cursor = index + word[0].length;
  }
  return html + escapeHtml(fragment.slice(cursor));
}

function byScore(left: Scored, right: Scored): number {
  return right.score - left.score || left.article.articleId.localeCompare(right.article.articleId);
}

function applyFilters(filters: SearchFilters | undefined): MockArticle[] {
  const category = filters?.category?.trim().toLowerCase();
  const source = filters?.source?.trim().toLowerCase();
  return CORPUS.filter(
    (article) =>
      (!category || article.category?.toLowerCase() === category) &&
      (!source || article.source?.toLowerCase() === source),
  );
}

const TITLE_BOOST = 4;
const TEXT_BOOST = 3;

// Same boosts as the backend (title 4, text 3). Two things stand in for the engines' different
// analyzers and BM25 statistics, so the two rankings agree mostly but not entirely:
//  - a per-engine factor on every score;
//  - a weak match (one hit in the text, none in the title) is kept by only some engines.
function rankLexical(articles: readonly MockArticle[], terms: readonly string[], engine: SearchEngine): Scored[] {
  return articles
    .map((article) => {
      const hits = countMatches(article.title, terms) * TITLE_BOOST + countMatches(article.text, terms) * TEXT_BOOST;
      const kept = hits > TEXT_BOOST || hash(`${engine}:weak:${article.articleId}`) < 0.55;
      return { article, score: kept ? hits * (0.8 + 0.4 * hash(`${engine}:bm25:${article.articleId}`)) : 0 };
    })
    .filter((scored) => scored.score > 0)
    .sort(byScore);
}

// Like the backend's placeholder embeddings: similarity is unrelated to meaning, and both
// engines see nearly the same values.
function rankVector(articles: readonly MockArticle[], queryText: string, engine: SearchEngine): Scored[] {
  return articles
    .map((article) => {
      const similarity = 0.5 + 0.08 * (hash(`${queryText}:${article.articleId}`) - 0.5);
      const engineNoise = 0.004 * (hash(`${engine}:knn:${article.articleId}`) - 0.5);
      return { article, score: similarity + engineNoise };
    })
    .sort(byScore);
}

interface Fused extends Scored {
  bm25Rank?: number;
  vectorRank?: number;
}

function fuse(lexical: readonly Scored[], vector: readonly Scored[], options: HybridOptions, topK: number): Fused[] {
  const bestLexical = lexical[0]?.score ?? 1;
  const fused = new Map<string, Fused & { bm25Score: number; vectorScore: number }>();

  lexical.slice(0, topK).forEach((scored, index) => {
    fused.set(scored.article.articleId, {
      article: scored.article,
      score: 0,
      bm25Rank: index + 1,
      bm25Score: scored.score,
      vectorScore: 0,
    });
  });
  vector.slice(0, topK).forEach((scored, index) => {
    const existing = fused.get(scored.article.articleId);
    if (existing) {
      existing.vectorRank = index + 1;
      existing.vectorScore = scored.score;
    } else {
      fused.set(scored.article.articleId, {
        article: scored.article,
        score: 0,
        vectorRank: index + 1,
        bm25Score: 0,
        vectorScore: scored.score,
      });
    }
  });

  return [...fused.values()]
    .map(({ bm25Score, vectorScore, ...entry }) => {
      const score =
        options.strategy === "Rrf"
          ? (entry.bm25Rank ? options.bm25Weight / (options.rrfK + entry.bm25Rank) : 0) +
            (entry.vectorRank ? options.vectorWeight / (options.rrfK + entry.vectorRank) : 0)
          : options.bm25Weight * (bm25Score / bestLexical) + options.vectorWeight * vectorScore;
      return { ...entry, score };
    })
    .sort(byScore);
}

function round(value: number, digits: number): number {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

function strategyName(request: SearchRequest, hybrid: HybridOptions): string {
  const elasticsearch = request.engine === "Elasticsearch";
  switch (request.mode) {
    case "Bm25":
      return elasticsearch ? "multi_match + match_phrase" : "$search compound";
    case "Vector":
      return elasticsearch ? "knn" : "$vectorSearch";
    case "Hybrid":
      if (hybrid.strategy === "Rrf") {
        return "reciprocal rank fusion";
      }
      return elasticsearch ? "script_score" : "$scoreFusion";
  }
}

export function mockSearch(request: SearchRequest): SearchResponse {
  const { text, directives } = parseDirectives(request.query);

  if (text === "") {
    const error: ApiError = { status: 400, title: "Query is required." };
    throw error;
  }
  if (!Number.isInteger(request.topK) || request.topK < 1 || request.topK > 100) {
    const error: ApiError = { status: 400, title: "TopK must be between 1 and 100." };
    throw error;
  }
  throwForErrorDirective(directives, request.engine);

  const terms = queryTerms(text);
  const articles = applyFilters(request.filters);
  const hybrid = request.hybrid ?? DEFAULT_HYBRID;

  let ranked: Fused[];
  switch (request.mode) {
    case "Bm25":
      ranked = rankLexical(articles, terms, request.engine);
      break;
    case "Vector":
      ranked = rankVector(articles, text, request.engine);
      break;
    case "Hybrid":
      ranked = fuse(
        rankLexical(articles, terms, request.engine),
        rankVector(articles, text, request.engine),
        hybrid,
        request.topK,
      );
      break;
  }

  const results: SearchResult[] = ranked.slice(0, request.topK).map((entry, index) => ({
    articleId: entry.article.articleId,
    title: entry.article.title,
    snippet: buildSnippet(entry.article.text, request.mode === "Vector" ? [] : terms),
    score: round(entry.score, request.mode === "Bm25" ? 3 : 5),
    rank: index + 1,
    source: entry.article.source,
    category: entry.article.category,
    bm25Rank: entry.bm25Rank,
    vectorRank: entry.vectorRank,
  }));

  const seed = hash(`${request.engine}:${request.mode}:${text}`);
  const lexicalOnly = request.mode === "Bm25";
  const engineMs = round((request.engine === "Elasticsearch" ? 7 : 12) + seed * (lexicalOnly ? 14 : 30), 1);
  const embeddingMs = lexicalOnly ? 0 : round(14 + hash(`embed:${text}`) * 12, 1);

  return {
    queryId: `mock-${Math.floor(seed * 0xffffffff).toString(16).padStart(8, "0")}`,
    query: request.query,
    engine: request.engine,
    mode: request.mode,
    elapsedMs: round(embeddingMs + engineMs + 1.2, 1),
    // BM25 leaves timings out, so the page's "no split available" path is exercised too.
    timings: lexicalOnly ? undefined : { embeddingMs, engineMs },
    results,
    diagnostics: request.includeDiagnostics
      ? {
          strategy: strategyName(request, hybrid),
          requestedTopK: request.topK,
          metadata: {
            ...(request.engine === "Elasticsearch"
              ? { index: "vector-hybrid-articles-dev" }
              : { database: "vector_hybrid_search_dev", collection: "article_chunks" }),
            ...(lexicalOnly ? {} : { embeddingModel: "sha256-placeholder", embeddingDimensions: "384" }),
            ...(request.mode === "Hybrid"
              ? {
                  bm25Weight: String(hybrid.bm25Weight),
                  vectorWeight: String(hybrid.vectorWeight),
                  ...(hybrid.strategy === "Rrf" ? { rrfK: String(hybrid.rrfK) } : {}),
                }
              : {}),
            source: "mock",
          },
        }
      : undefined,
  };
}
