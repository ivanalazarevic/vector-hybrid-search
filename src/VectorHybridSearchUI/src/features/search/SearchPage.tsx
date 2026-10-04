import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { mockExamples, search, toApiError, type MockExample } from "../../api";
import page from "../../app/page.module.css";
import { EmptyState } from "../../components/EmptyState";
import { EngineBadge } from "../../components/EngineBadge";
import { ErrorPanel } from "../../components/ErrorPanel";
import { LatencyBadge } from "../../components/LatencyBadge";
import { MockNotice } from "../../components/MockNotice";
import { QueryForm } from "../../components/QueryForm";
import { ResultCard } from "../../components/ResultCard";
import { collectFacets } from "../../lib/facets";
import { MODE_LABEL } from "../../lib/labels";
import { parseQueryState, toSearchParams, toSearchRequest, type QueryState } from "../../lib/queryState";
import { DiagnosticsPanel } from "./DiagnosticsPanel";
import styles from "./SearchPage.module.css";

export function SearchPage() {
  // The URL is the submitted query: reloading or sharing the link repeats the same search.
  const [params, setParams] = useSearchParams();
  const state = useMemo(() => parseQueryState(params), [params]);
  const request = useMemo(() => toSearchRequest(state), [state]);
  const hasQuery = request.query !== "";

  const result = useQuery({
    queryKey: ["search", request],
    queryFn: ({ signal }) => search(request, signal),
    enabled: hasQuery,
    placeholderData: keepPreviousData,
  });
  const response = hasQuery ? result.data : undefined;
  const facets = useMemo(() => collectFacets(response?.results ?? []), [response]);

  const submit = (next: QueryState) => {
    const nextParams = toSearchParams(next, { includeEngine: true });
    if (hasQuery && nextParams.toString() === toSearchParams(state, { includeEngine: true }).toString()) {
      void result.refetch();
    } else {
      setParams(nextParams);
    }
  };

  const pickExample = (example: MockExample) => submit({ ...state, query: example.query, mode: example.mode ?? "Bm25" });

  return (
    <div className={page.page}>
      <div className={page.intro}>
        <h1 className={page.heading}>Search</h1>
        <p className={page.lede}>Rank articles with one engine and one retrieval mode.</p>
      </div>

      <QueryForm
        submitted={state}
        showEngine
        submitLabel="Search"
        categories={facets.categories}
        sources={facets.sources}
        onSubmit={submit}
      />

      <MockNotice examples={mockExamples("search")} open={!hasQuery} onPick={pickExample} />

      {!hasQuery ? (
        <EmptyState title="No query yet">
          Type a query and press Enter. Engine, mode and filters can be changed afterwards without retyping it.
        </EmptyState>
      ) : result.isError ? (
        <ErrorPanel error={toApiError(result.error)} onRetry={() => void result.refetch()} />
      ) : !response ? (
        <p className={page.status} role="status">
          Searching…
        </p>
      ) : (
        <section
          className={result.isPlaceholderData ? page.stale : undefined}
          aria-busy={result.isFetching}
          aria-label="Results"
        >
          <div className={styles.panel}>
            <div className={styles.summary}>
              <h2 className={styles.count} aria-live="polite">
                {response.results.length} {response.results.length === 1 ? "result" : "results"}
              </h2>
              <span className={styles.chip}>
                <EngineBadge engine={response.engine} />
              </span>
              <span className={styles.chip}>{MODE_LABEL[response.mode]}</span>
              <span className={styles.latency}>
                <LatencyBadge elapsedMs={response.elapsedMs} timings={response.timings} />
              </span>
            </div>

            {response.results.length === 0 ? (
              <div className={styles.message}>
                <EmptyState title={`No articles match “${response.query}”`}>
                  {response.mode === "Bm25"
                    ? "BM25 only returns articles that contain the query terms. Try other words, or switch to vector or hybrid mode."
                    : "Vector and hybrid modes return the nearest articles whenever any exist, so either the filters exclude everything or no articles are ingested."}
                </EmptyState>
              </div>
            ) : (
              <ol>
                {response.results.map((item) => (
                  <ResultCard key={item.articleId} result={item} showContributions={response.mode === "Hybrid"} />
                ))}
              </ol>
            )}
          </div>

          {response.diagnostics && <DiagnosticsPanel queryId={response.queryId} diagnostics={response.diagnostics} />}
        </section>
      )}
    </div>
  );
}
