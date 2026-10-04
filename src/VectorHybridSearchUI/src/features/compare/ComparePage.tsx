import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import {
  compare,
  mockExamples,
  toApiError,
  type EngineComparisonEntry,
  type MockExample,
  type SearchEngine,
  type SearchMode,
} from "../../api";
import page from "../../app/page.module.css";
import { EmptyState } from "../../components/EmptyState";
import { EngineBadge, EngineGlyph } from "../../components/EngineBadge";
import { ErrorPanel } from "../../components/ErrorPanel";
import { LatencyBadge } from "../../components/LatencyBadge";
import { MockNotice } from "../../components/MockNotice";
import { QueryForm } from "../../components/QueryForm";
import { ResultCard } from "../../components/ResultCard";
import { collectFacets } from "../../lib/facets";
import { ENGINE_LABEL, MODE_LABEL } from "../../lib/labels";
import { parseQueryState, toCompareRequest, toSearchParams, type QueryState } from "../../lib/queryState";
import { describeDelta, rankDiff, type RankDiff } from "../../lib/rankDiff";
import styles from "./ComparePage.module.css";
import { RankBridge } from "./RankBridge";

function RankNote({ diff, engine, other }: { diff: RankDiff; engine: SearchEngine; other: SearchEngine }) {
  if (diff.status === "unique") {
    return (
      <p className={styles.unique} data-engine={engine}>
        <EngineGlyph engine={engine} />
        Only in {ENGINE_LABEL[engine]}
      </p>
    );
  }

  const arrow = diff.delta > 0 ? "▲" : diff.delta < 0 ? "▼" : "=";
  return (
    <p className={styles.shared}>
      Rank {diff.otherRank} in {ENGINE_LABEL[other]}
      <span className={styles.delta}>
        <span aria-hidden="true">{arrow} </span>
        {describeDelta(diff.delta)}
        {diff.delta !== 0 && " here"}
      </span>
    </p>
  );
}

interface EngineColumnProps {
  engine: SearchEngine;
  other: SearchEngine;
  entry: EngineComparisonEntry | undefined;
  mode: SearchMode;
  /** Absent when the other engine failed, because then there is nothing to compare against. */
  diffs: Map<string, RankDiff> | undefined;
}

function EngineColumn({ engine, other, entry, mode, diffs }: EngineColumnProps) {
  const results = entry?.response?.results;

  return (
    <section className={styles.column} data-engine={engine} aria-label={`${ENGINE_LABEL[engine]} results`}>
      <header className={styles.columnHead}>
        <EngineBadge engine={engine} size="lg" />
        {results && (
          <span className={styles.columnCount}>
            {results.length} {results.length === 1 ? "result" : "results"}
          </span>
        )}
      </header>

      {!results ? (
        <div className={styles.columnMessage}>
          <ErrorPanel
            error={{
              title: `${ENGINE_LABEL[engine]} did not return results`,
              detail: entry?.error ?? "The comparison response has no entry for this engine.",
            }}
          />
        </div>
      ) : results.length === 0 ? (
        <div className={styles.columnMessage}>
          <EmptyState title="No matching articles" />
        </div>
      ) : (
        <ol className={styles.results}>
          {results.map((result) => {
            const diff = diffs?.get(result.articleId);
            return (
              <ResultCard
                key={result.articleId}
                result={result}
                compact
                showContributions={mode === "Hybrid"}
                annotation={diff && <RankNote diff={diff} engine={engine} other={other} />}
              />
            );
          })}
        </ol>
      )}
    </section>
  );
}

export function ComparePage() {
  const [params, setParams] = useSearchParams();
  const state = useMemo(() => parseQueryState(params), [params]);
  const request = useMemo(() => toCompareRequest(state), [state]);
  const hasQuery = request.query !== "";

  const result = useQuery({
    queryKey: ["compare", request],
    queryFn: ({ signal }) => compare(request, signal),
    enabled: hasQuery,
    placeholderData: keepPreviousData,
  });
  const response = hasQuery ? result.data : undefined;

  const elasticsearch = response?.engines.find((entry) => entry.engine === "Elasticsearch");
  const mongoDb = response?.engines.find((entry) => entry.engine === "MongoDbAtlas");
  const elasticsearchResults = elasticsearch?.response?.results;
  const mongoDbResults = mongoDb?.response?.results;
  const comparable = elasticsearchResults !== undefined && mongoDbResults !== undefined;

  const facets = useMemo(
    () => collectFacets([...(elasticsearchResults ?? []), ...(mongoDbResults ?? [])]),
    [elasticsearchResults, mongoDbResults],
  );
  const diffs = useMemo(
    () =>
      comparable
        ? {
            elasticsearch: rankDiff(elasticsearchResults, mongoDbResults),
            mongoDb: rankDiff(mongoDbResults, elasticsearchResults),
          }
        : undefined,
    [comparable, elasticsearchResults, mongoDbResults],
  );

  const submit = (next: QueryState) => {
    const nextParams = toSearchParams(next, { includeEngine: false });
    if (hasQuery && nextParams.toString() === toSearchParams(state, { includeEngine: false }).toString()) {
      void result.refetch();
    } else {
      setParams(nextParams);
    }
  };

  const pickExample = (example: MockExample) => submit({ ...state, query: example.query, mode: example.mode ?? "Bm25" });

  const showBridge = comparable && elasticsearchResults.length + mongoDbResults.length > 0;

  return (
    <div className={page.page}>
      <div className={page.intro}>
        <h1 className={page.heading}>Compare</h1>
        <p className={page.lede}>Run one query on both engines and read the rankings side by side.</p>
      </div>

      <QueryForm
        submitted={state}
        showEngine={false}
        submitLabel="Compare"
        categories={facets.categories}
        sources={facets.sources}
        onSubmit={submit}
      />

      <MockNotice examples={mockExamples("compare")} open={!hasQuery} onPick={pickExample} />

      {!hasQuery ? (
        <EmptyState title="No query yet">
          Type a query and press Enter. Both engines run it with the same mode, top K and filters.
        </EmptyState>
      ) : result.isError ? (
        <ErrorPanel error={toApiError(result.error)} onRetry={() => void result.refetch()} />
      ) : !response ? (
        <p className={page.status} role="status">
          Running the query on both engines…
        </p>
      ) : (
        <section
          className={`${styles.comparison} ${result.isPlaceholderData ? page.stale : ""}`}
          aria-busy={result.isFetching}
          aria-label="Comparison"
        >
          <dl className={styles.summary}>
            <div className={styles.stat}>
              <dt className={styles.statName}>Shared articles</dt>
              <dd className={styles.statValue}>{comparable ? response.overlap.sharedCount : "n/a"}</dd>
            </div>
            <div className={styles.stat}>
              <dt className={styles.statName}>Jaccard overlap</dt>
              <dd className={styles.statValue}>{comparable ? response.overlap.jaccard.toFixed(2) : "n/a"}</dd>
            </div>
            <div className={styles.stat}>
              <dt className={styles.statName}>Mode</dt>
              <dd className={styles.statValue}>{MODE_LABEL[response.mode]}</dd>
            </div>
            {[elasticsearch, mongoDb].map(
              (entry) =>
                entry && (
                  <div key={entry.engine} className={styles.stat}>
                    <dt className={styles.statName}>
                      <EngineBadge engine={entry.engine} /> latency
                    </dt>
                    <dd>
                      {entry.response ? (
                        <LatencyBadge elapsedMs={entry.response.elapsedMs} timings={entry.response.timings} />
                      ) : (
                        <span className={styles.failed}>failed</span>
                      )}
                    </dd>
                  </div>
                ),
            )}
          </dl>

          <div className={showBridge ? styles.columnsWithBridge : styles.columns}>
            <EngineColumn
              engine="Elasticsearch"
              other="MongoDbAtlas"
              entry={elasticsearch}
              mode={response.mode}
              diffs={diffs?.elasticsearch}
            />
            {showBridge && <RankBridge elasticsearch={elasticsearchResults} mongoDb={mongoDbResults} />}
            <EngineColumn
              engine="MongoDbAtlas"
              other="Elasticsearch"
              entry={mongoDb}
              mode={response.mode}
              diffs={diffs?.mongoDb}
            />
          </div>
        </section>
      )}
    </div>
  );
}
