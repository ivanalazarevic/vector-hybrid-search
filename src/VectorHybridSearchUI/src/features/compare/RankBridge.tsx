import type { SearchResult } from "../../api";
import { EngineGlyph } from "../../components/EngineBadge";
import styles from "./RankBridge.module.css";

const WIDTH = 200;
const LEFT_X = 46;
const RIGHT_X = 154;
const PADDING = 8;

interface RankBridgeProps {
  elasticsearch: readonly SearchResult[];
  mongoDb: readonly SearchResult[];
}

/**
 * A slopegraph of the two rankings: Elasticsearch ranks down the left, MongoDB ranks down the
 * right, one line per article both engines returned. Flat lines are agreement; steep lines are
 * disagreement; hollow marks are articles only one engine returned.
 */
export function RankBridge({ elasticsearch, mongoDb }: RankBridgeProps) {
  const rows = Math.max(elasticsearch.length, mongoDb.length);
  const rowHeight = rows <= 12 ? 26 : rows <= 25 ? 18 : rows <= 50 ? 10 : 6;
  const labelEvery = rowHeight >= 18 ? 1 : rowHeight >= 10 ? 5 : 10;
  const mark = Math.min(11, rowHeight - 2);
  const height = rows * rowHeight + PADDING * 2;

  const y = (rank: number) => PADDING + (rank - 0.5) * rowHeight;
  const showLabel = (rank: number) => rank === 1 || rank % labelEvery === 0;

  const mongoRanks = new Map(mongoDb.map((result) => [result.articleId, result.rank]));
  const elasticsearchIds = new Set(elasticsearch.map((result) => result.articleId));
  const shared = elasticsearch.filter((result) => mongoRanks.has(result.articleId));

  return (
    <figure className={styles.bridge}>
      <div className={styles.axes} aria-hidden="true">
        <span className={styles.axis}>
          <EngineGlyph engine="Elasticsearch" />
          rank
        </span>
        <span className={styles.axis}>
          rank
          <EngineGlyph engine="MongoDbAtlas" />
        </span>
      </div>
      <svg
        className={styles.chart}
        viewBox={`0 0 ${WIDTH} ${height}`}
        role="img"
        aria-label={`Rank comparison: ${shared.length} of ${elasticsearch.length} Elasticsearch results also appear in the MongoDB results.`}
      >
        {shared.map((result) => {
          const mongoRank = mongoRanks.get(result.articleId)!;
          return (
            <line
              key={result.articleId}
              className={styles.link}
              x1={LEFT_X}
              y1={y(result.rank)}
              x2={RIGHT_X}
              y2={y(mongoRank)}
            >
              <title>{`${result.title}: Elasticsearch rank ${result.rank}, MongoDB rank ${mongoRank}`}</title>
            </line>
          );
        })}

        <g data-engine="Elasticsearch">
          {elasticsearch.map((result) => (
            <g key={result.articleId}>
              <rect
                className={mongoRanks.has(result.articleId) ? styles.markShared : styles.markUnique}
                x={LEFT_X - mark / 2}
                y={y(result.rank) - mark / 2}
                width={mark}
                height={mark}
              />
              {showLabel(result.rank) && (
                <text className={styles.rank} x={LEFT_X - mark / 2 - 8} y={y(result.rank)} textAnchor="end">
                  {result.rank}
                </text>
              )}
            </g>
          ))}
        </g>

        <g data-engine="MongoDbAtlas">
          {mongoDb.map((result) => (
            <g key={result.articleId}>
              <circle
                className={elasticsearchIds.has(result.articleId) ? styles.markShared : styles.markUnique}
                cx={RIGHT_X}
                cy={y(result.rank)}
                r={mark / 2}
              />
              {showLabel(result.rank) && (
                <text className={styles.rank} x={RIGHT_X + mark / 2 + 8} y={y(result.rank)} textAnchor="start">
                  {result.rank}
                </text>
              )}
            </g>
          ))}
        </g>
      </svg>
      <figcaption className={styles.caption}>
        A line joins an article both engines returned; the steeper it is, the more the ranks differ. Hollow marks were
        returned by one engine only.
      </figcaption>
    </figure>
  );
}
