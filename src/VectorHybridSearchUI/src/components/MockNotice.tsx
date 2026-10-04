import type { MockExample } from "../api";
import { MODE_LABEL } from "../lib/labels";
import styles from "./MockNotice.module.css";

interface MockNoticeProps {
  examples: readonly MockExample[];
  /** Open while there is nothing else on the page; collapsed once results are showing. */
  open: boolean;
  onPick: (example: MockExample) => void;
}

/** Shown only while a feature runs on mock data: lists queries that reach each prepared case. */
export function MockNotice({ examples, open, onPick }: MockNoticeProps) {
  if (examples.length === 0) {
    return null;
  }

  return (
    <details className={styles.notice} open={open}>
      <summary className={styles.summary}>Mock data: example queries</summary>
      <p className={styles.intro}>
        This page answers from built-in sample articles, not from the backend. A #tag in the query selects a prepared
        case.
      </p>
      <ul className={styles.list}>
        {examples.map((example) => (
          <li key={`${example.query}:${example.mode ?? ""}`} className={styles.item}>
            <button type="button" className={styles.example} onClick={() => onPick(example)}>
              {example.query}
              {example.mode && `, ${MODE_LABEL[example.mode]} mode`}
            </button>
            <span className={styles.shows}>{example.shows}</span>
          </li>
        ))}
      </ul>
    </details>
  );
}
