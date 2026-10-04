import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { getRun, getStorage, listRuns, toApiError } from "../../api";
import page from "../../app/page.module.css";
import { EmptyState } from "../../components/EmptyState";
import { ErrorPanel } from "../../components/ErrorPanel";
import { Segmented } from "../../components/Segmented";
import styles from "./AnalyticsPage.module.css";
import { MetricCharts } from "./MetricCharts";
import { MetricTable } from "./MetricTable";
import { RunPicker } from "./RunPicker";
import { StorageCards } from "./StorageCards";

const PREFERRED_K = 10;

export function AnalyticsPage() {
  // ?run=<id>&k=<n> keeps the selected run and cut-off in the URL, like the query on the other pages.
  const [params, setParams] = useSearchParams();

  const runs = useQuery({ queryKey: ["evaluation-runs"], queryFn: ({ signal }) => listRuns(signal) });
  const storage = useQuery({ queryKey: ["storage"], queryFn: ({ signal }) => getStorage(signal) });

  const requestedRunId = params.get("run");
  const runId = runs.data?.find((run) => run.runId === requestedRunId)?.runId ?? runs.data?.[0]?.runId;

  const run = useQuery({
    queryKey: ["evaluation-run", runId],
    queryFn: ({ signal }) => getRun(runId!, signal),
    enabled: runId !== undefined,
  });

  const availableK = run.data?.k ?? [];
  const requestedK = Number(params.get("k"));
  const k = availableK.includes(requestedK)
    ? requestedK
    : availableK.includes(PREFERRED_K)
      ? PREFERRED_K
      : availableK[0];
  const rows = run.data?.rows.filter((row) => row.k === k) ?? [];

  const select = (next: { run?: string; k?: number }) => {
    const nextParams = new URLSearchParams(params);
    if (next.run !== undefined) {
      nextParams.set("run", next.run);
      nextParams.delete("k");
    }
    if (next.k !== undefined) {
      nextParams.set("k", String(next.k));
    }
    setParams(nextParams);
  };

  return (
    <div className={page.page}>
      <div className={page.intro}>
        <h1 className={page.heading}>Analytics</h1>
        <p className={page.lede}>Retrieval quality and latency from the evaluation runs.</p>
      </div>

      {runs.isError ? (
        <ErrorPanel error={toApiError(runs.error)} onRetry={() => void runs.refetch()} />
      ) : !runs.data ? (
        <p className={page.status} role="status">
          Loading evaluation runs…
        </p>
      ) : runs.data.length === 0 ? (
        <EmptyState title="No evaluation runs yet">
          Start an evaluation from the backend. Each finished run appears here with its metrics.
        </EmptyState>
      ) : (
        <>
          <RunPicker runs={runs.data} selectedRunId={runId} onSelect={(next) => select({ run: next })} />

          {run.isError ? (
            <ErrorPanel error={toApiError(run.error)} onRetry={() => void run.refetch()} />
          ) : !run.data || k === undefined ? (
            <p className={page.status} role="status">
              Loading the run…
            </p>
          ) : rows.length === 0 ? (
            <EmptyState title="This run has no metric rows">The run finished without recording any results.</EmptyState>
          ) : (
            <>
              <section className={styles.section} aria-label="Metric table">
                <MetricTable
                  // A new run or K starts again from the run's own row order.
                  key={`${run.data.runId}:${k}`}
                  rows={rows}
                  k={k}
                  toolbar={
                    <Segmented
                      legend="Cut-off K"
                      name="cutoff"
                      value={String(k)}
                      options={availableK.map((value) => ({ value: String(value), label: String(value) }))}
                      onChange={(value) => select({ k: Number(value) })}
                    />
                  }
                />
              </section>

              <section className={styles.section} aria-label="Charts">
                <h2 className={styles.heading}>Charts</h2>
                <MetricCharts rows={rows} k={k} />
              </section>
            </>
          )}
        </>
      )}

      <section className={styles.section} aria-label="Index storage">
        <h2 className={styles.heading}>Index storage and indexing time</h2>
        {storage.isError ? (
          <ErrorPanel error={toApiError(storage.error)} onRetry={() => void storage.refetch()} />
        ) : !storage.data ? (
          <p className={page.status} role="status">
            Loading storage figures…
          </p>
        ) : (
          <StorageCards stats={storage.data} />
        )}
      </section>
    </div>
  );
}
