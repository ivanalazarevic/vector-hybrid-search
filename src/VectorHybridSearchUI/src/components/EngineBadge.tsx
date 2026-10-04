import type { SearchEngine } from "../api";
import { ENGINE_LABEL } from "../lib/labels";
import styles from "./EngineBadge.module.css";

/** The engine's shape: a square for Elasticsearch, a circle for MongoDB. Always paired with the engine colour. */
export function EngineGlyph({ engine }: { engine: SearchEngine }) {
  return (
    <svg className={styles.glyph} data-engine={engine} viewBox="0 0 12 12" aria-hidden="true">
      {engine === "Elasticsearch" ? <rect x="1" y="1" width="10" height="10" /> : <circle cx="6" cy="6" r="5.5" />}
    </svg>
  );
}

interface EngineBadgeProps {
  engine: SearchEngine;
  size?: "md" | "lg";
}

export function EngineBadge({ engine, size = "md" }: EngineBadgeProps) {
  return (
    <span className={size === "lg" ? styles.badgeLarge : styles.badge}>
      <EngineGlyph engine={engine} />
      {ENGINE_LABEL[engine]}
    </span>
  );
}
