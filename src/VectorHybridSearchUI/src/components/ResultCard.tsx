import type { ReactNode } from "react";
import type { SearchResult } from "../api";
import { formatScore } from "../lib/format";
import styles from "./ResultCard.module.css";
import { Snippet } from "./Snippet";

interface ResultCardProps {
  result: SearchResult;
  /** Hybrid only: show the BM25 and vector ranks that produced the final rank. */
  showContributions?: boolean;
  /** Narrower layout for the side-by-side columns. */
  compact?: boolean;
  /** Extra line under the meta row, e.g. the rank difference on the compare page. */
  annotation?: ReactNode;
}

function Contribution({ label, rank }: { label: string; rank: number | undefined }) {
  if (rank === undefined) {
    return <span className={styles.contributionMissing}>No {label} rank</span>;
  }
  return (
    <span className={styles.contribution}>
      {label} rank <strong>{rank}</strong>
    </span>
  );
}

export function ResultCard({ result, showContributions = false, compact = false, annotation }: ResultCardProps) {
  return (
    <li className={compact ? styles.cardCompact : styles.card}>
      <span className={styles.rank}>
        <span className="visually-hidden">Rank </span>
        {result.rank}
      </span>
      <div className={styles.body}>
        <h3 className={styles.title}>{result.title}</h3>
        <Snippet html={result.snippet} />
        <div className={styles.meta}>
          {result.category && <span className={styles.category}>{result.category}</span>}
          {result.source && <span>{result.source}</span>}
          {showContributions && (
            <>
              <Contribution label="BM25" rank={result.bm25Rank} />
              <Contribution label="Vector" rank={result.vectorRank} />
            </>
          )}
          <span className={styles.articleId}>{result.articleId}</span>
        </div>
        {annotation}
      </div>
      <div className={styles.score}>
        <span className={styles.scoreLabel}>Score</span>
        <span className={styles.scoreValue}>{formatScore(result.score)}</span>
      </div>
    </li>
  );
}
