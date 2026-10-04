import { useId, useState, type FormEvent } from "react";
import { DEFAULT_QUERY_STATE, type QueryState } from "../lib/queryState";
import controls from "./controls.module.css";
import { EngineSelect } from "./EngineSelect";
import { HybridControls, type HybridDraft } from "./HybridControls";
import { ModeSelect } from "./ModeSelect";
import styles from "./QueryForm.module.css";

interface Draft extends Omit<QueryState, "topK" | "hybrid"> {
  topK: string;
  hybrid: HybridDraft;
}

function toDraft(state: QueryState): Draft {
  return {
    ...state,
    topK: String(state.topK),
    hybrid: {
      strategy: state.hybrid.strategy,
      bm25Weight: String(state.hybrid.bm25Weight),
      vectorWeight: String(state.hybrid.vectorWeight),
      rrfK: String(state.hybrid.rrfK),
    },
  };
}

function toNumber(text: string, min: number, max: number, fallback: number): number {
  const parsed = text.trim() === "" ? Number.NaN : Number(text);
  return Number.isFinite(parsed) ? Math.min(max, Math.max(min, parsed)) : fallback;
}

function toState(draft: Draft): QueryState {
  const defaults = DEFAULT_QUERY_STATE;
  return {
    ...draft,
    topK: Math.round(toNumber(draft.topK, 1, 100, defaults.topK)),
    hybrid: {
      strategy: draft.hybrid.strategy,
      bm25Weight: toNumber(draft.hybrid.bm25Weight, 0, 10, defaults.hybrid.bm25Weight),
      vectorWeight: toNumber(draft.hybrid.vectorWeight, 0, 10, defaults.hybrid.vectorWeight),
      rrfK: Math.round(toNumber(draft.hybrid.rrfK, 1, 1000, defaults.hybrid.rrfK)),
    },
  };
}

interface QueryFormProps {
  /** The submitted state (from the URL). The form edits a draft of it. */
  submitted: QueryState;
  showEngine: boolean;
  submitLabel: string;
  /** Filter suggestions: values seen in the current results. */
  categories: readonly string[];
  sources: readonly string[];
  onSubmit: (state: QueryState) => void;
}

export function QueryForm({ submitted, showEngine, submitLabel, categories, sources, onSubmit }: QueryFormProps) {
  const id = useId();
  const [draft, setDraft] = useState(() => toDraft(submitted));

  // Back/forward or an example link changes the submitted state from outside: restart the draft from it.
  const submittedKey = JSON.stringify(submitted);
  const [syncedKey, setSyncedKey] = useState(submittedKey);
  if (syncedKey !== submittedKey) {
    setSyncedKey(submittedKey);
    setDraft(toDraft(submitted));
  }

  const edit = (patch: Partial<Draft>) => setDraft((current) => ({ ...current, ...patch }));

  // Switches re-run an already submitted query at once; text and number fields wait for Enter or the button.
  const switchTo = (patch: Partial<Draft>) => {
    const next = { ...draft, ...patch };
    setDraft(next);
    if (submitted.query.trim() !== "" && next.query.trim() !== "") {
      onSubmit(toState(next));
    }
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit(toState(draft));
  };

  return (
    <form className={styles.form} role="search" onSubmit={handleSubmit}>
      <div className={styles.queryRow}>
        <label className="visually-hidden" htmlFor={`${id}-query`}>
          Query
        </label>
        <div className={styles.queryBox}>
          <svg className={styles.queryIcon} viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
          <input
            id={`${id}-query`}
            className={`${controls.input} ${styles.query}`}
            type="search"
            placeholder="Search articles, for example: interest rates"
            autoComplete="off"
            autoFocus
            value={draft.query}
            onChange={(event) => edit({ query: event.target.value })}
          />
        </div>
        <button type="submit" className={`${controls.button} ${styles.submit}`}>
          {submitLabel}
        </button>
      </div>

      <div className={styles.optionsRow}>
        {showEngine && <EngineSelect value={draft.engine} onChange={(engine) => switchTo({ engine })} />}
        <ModeSelect value={draft.mode} onChange={(mode) => switchTo({ mode })} />
        <label className={controls.field}>
          <span className={controls.label}>Top K</span>
          <input
            className={`${controls.input} ${styles.topK}`}
            type="number"
            min={1}
            max={100}
            step={1}
            value={draft.topK}
            onChange={(event) => edit({ topK: event.target.value })}
          />
        </label>
        <label className={controls.field}>
          <span className={controls.label}>Category</span>
          <input
            className={`${controls.input} ${styles.filter}`}
            type="text"
            placeholder="Any"
            list={`${id}-categories`}
            value={draft.category}
            onChange={(event) => edit({ category: event.target.value })}
          />
        </label>
        <label className={controls.field}>
          <span className={controls.label}>Source</span>
          <input
            className={`${controls.input} ${styles.filter}`}
            type="text"
            placeholder="Any"
            list={`${id}-sources`}
            value={draft.source}
            onChange={(event) => edit({ source: event.target.value })}
          />
        </label>
        <datalist id={`${id}-categories`}>
          {categories.map((category) => (
            <option key={category} value={category} />
          ))}
        </datalist>
        <datalist id={`${id}-sources`}>
          {sources.map((source) => (
            <option key={source} value={source} />
          ))}
        </datalist>
      </div>

      {draft.mode === "Hybrid" && (
        <HybridControls
          value={draft.hybrid}
          onStrategyChange={(strategy) => switchTo({ hybrid: { ...draft.hybrid, strategy } })}
          onNumberChange={(field, value) => edit({ hybrid: { ...draft.hybrid, [field]: value } })}
        />
      )}
    </form>
  );
}
