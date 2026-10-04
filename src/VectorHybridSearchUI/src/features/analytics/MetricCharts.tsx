import { useMemo, type ReactNode } from "react";
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { EvaluationSummaryRow } from "../../api";
import { EngineGlyph } from "../../components/EngineBadge";
import { formatMetric } from "../../lib/format";
import { ENGINE_LABEL } from "../../lib/labels";
import { ENGINES } from "../../lib/queryState";
import { configurationLabel } from "./configuration";
import styles from "./MetricCharts.module.css";

// Chart encoding, the same in all three charts:
//   colour + fill  -> engine (Elasticsearch solid, MongoDB hatched, so it also reads in greyscale)
//   row            -> configuration (mode and fusion settings)
//   opacity        -> latency only: solid up to p50, pale from p50 to p95
const HATCH_ID = "mongo-hatch";
const FILL = { Elasticsearch: "var(--es)", MongoDbAtlas: `url(#${HATCH_ID})` } as const;
const PALE = { Elasticsearch: "var(--es)", MongoDbAtlas: "var(--mongo)" } as const;

const BAR_SIZE = 18;
const ROW_HEIGHT = 62;
const AXIS_TICK = { fill: "var(--ink-2)", fontSize: 14 };
const VALUE_LABEL = { fill: "var(--ink)", fontSize: 14, fontWeight: 600 };

/** One chart row: a configuration with each engine's values (absent when the run did not cover it). */
interface ChartRow {
  label: string;
  Elasticsearch?: EvaluationSummaryRow;
  MongoDbAtlas?: EvaluationSummaryRow;
}

function toChartRows(rows: readonly EvaluationSummaryRow[]): ChartRow[] {
  const byLabel = new Map<string, ChartRow>();
  for (const row of rows) {
    const label = configurationLabel(row);
    const chartRow = byLabel.get(label) ?? { label };
    chartRow[row.engine] = row;
    byLabel.set(label, chartRow);
  }
  return [...byLabel.values()];
}

/** Defines the MongoDB hatch once; every chart on the page refers to it by id. */
function HatchPattern() {
  return (
    <svg className={styles.defs} aria-hidden="true">
      <defs>
        <pattern id={HATCH_ID} width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
          <rect width="6" height="6" fill="var(--mongo)" />
          <rect width="2" height="6" fill="var(--surface)" />
        </pattern>
      </defs>
    </svg>
  );
}

function Swatch({ fill, opacity = 1 }: { fill: string; opacity?: number }) {
  return (
    <svg className={styles.swatch} viewBox="0 0 28 14" aria-hidden="true">
      <rect width="28" height="14" rx="2" fill={fill} fillOpacity={opacity} />
    </svg>
  );
}

interface TooltipProps {
  active?: boolean;
  payload?: { payload: ChartRow }[];
  describe: (row: EvaluationSummaryRow) => string;
}

function ChartTooltip({ active, payload, describe }: TooltipProps) {
  const row = payload?.[0]?.payload;
  if (!active || !row) {
    return null;
  }
  return (
    <div className={styles.tooltip}>
      <p className={styles.tooltipTitle}>{row.label}</p>
      {ENGINES.map((engine) => {
        const values = row[engine];
        return (
          <p key={engine} className={styles.tooltipLine}>
            <EngineGlyph engine={engine} />
            <span>{ENGINE_LABEL[engine]}</span>
            <strong>{values ? describe(values) : "not in this run"}</strong>
          </p>
        );
      })}
    </div>
  );
}

interface ChartFrameProps {
  title: string;
  rows: readonly ChartRow[];
  /** Right margin: room for the value labels at the bar ends. */
  labelRoom: number;
  domain?: [number, number];
  describe: (row: EvaluationSummaryRow) => string;
  children: ReactNode;
}

function ChartFrame({ title, rows, labelRoom, domain, describe, children }: ChartFrameProps) {
  return (
    <figure className={styles.chart}>
      <figcaption className={styles.title}>{title}</figcaption>
      <ResponsiveContainer width="100%" height={rows.length * ROW_HEIGHT + 36}>
        <BarChart
          layout="vertical"
          data={rows as ChartRow[]}
          margin={{ top: 0, right: labelRoom, bottom: 0, left: 0 }}
          barGap={3}
          barCategoryGap={8}
        >
          <CartesianGrid horizontal={false} stroke="var(--rule)" />
          <XAxis type="number" domain={domain} tick={AXIS_TICK} axisLine={false} tickLine={false} />
          <YAxis
            type="category"
            dataKey="label"
            width={170}
            tick={{ fill: "var(--ink)", fontSize: 15 }}
            axisLine={{ stroke: "var(--rule-strong)" }}
            tickLine={false}
          />
          <Tooltip
            cursor={{ fill: "var(--surface-sunk)" }}
            isAnimationActive={false}
            content={<ChartTooltip describe={describe} />}
          />
          {children}
        </BarChart>
      </ResponsiveContainer>
    </figure>
  );
}

function QualityChart({ title, rows, metric }: { title: string; rows: readonly ChartRow[]; metric: "ndcgAtK" | "mrr" }) {
  return (
    <ChartFrame title={title} rows={rows} labelRoom={56} domain={[0, 1]} describe={(row) => formatMetric(row[metric])}>
      {ENGINES.map((engine) => (
        <Bar
          key={engine}
          dataKey={(row: ChartRow) => row[engine]?.[metric]}
          name={ENGINE_LABEL[engine]}
          fill={FILL[engine]}
          barSize={BAR_SIZE}
          radius={[0, 5, 5, 0]}
          isAnimationActive={false}
        >
          <LabelList
            position="right"
            formatter={(value: number) => formatMetric(value)}
            {...VALUE_LABEL}
          />
        </Bar>
      ))}
    </ChartFrame>
  );
}

function LatencyChart({ rows }: { rows: readonly ChartRow[] }) {
  return (
    <ChartFrame
      title="Latency by configuration, milliseconds"
      rows={rows}
      labelRoom={120}
      describe={(row) => `p50 ${row.latencyP50Ms.toFixed(1)} ms, p95 ${row.latencyP95Ms.toFixed(1)} ms`}
    >
      {ENGINES.flatMap((engine) => [
        <Bar
          key={`${engine}-p50`}
          dataKey={(row: ChartRow) => row[engine]?.latencyP50Ms}
          stackId={engine}
          fill={FILL[engine]}
          barSize={BAR_SIZE}
          isAnimationActive={false}
        />,
        <Bar
          key={`${engine}-p95`}
          dataKey={(row: ChartRow) => {
            const values = row[engine];
            return values && values.latencyP95Ms - values.latencyP50Ms;
          }}
          stackId={engine}
          fill={PALE[engine]}
          fillOpacity={0.3}
          barSize={BAR_SIZE}
          radius={[0, 5, 5, 0]}
          isAnimationActive={false}
        >
          <LabelList
            position="right"
            valueAccessor={(entry: { payload?: ChartRow }) => {
              const values = entry.payload?.[engine];
              return values ? `${values.latencyP50Ms.toFixed(1)} / ${values.latencyP95Ms.toFixed(1)}` : "";
            }}
            {...VALUE_LABEL}
          />
        </Bar>,
      ])}
    </ChartFrame>
  );
}

interface MetricChartsProps {
  /** Rows of one run at one K. */
  rows: readonly EvaluationSummaryRow[];
  k: number;
}

export function MetricCharts({ rows, k }: MetricChartsProps) {
  const chartRows = useMemo(() => toChartRows(rows), [rows]);

  return (
    <div className={styles.charts}>
      <HatchPattern />
      <ul className={styles.legend} aria-label="Chart legend">
        {ENGINES.map((engine) => (
          <li key={engine} className={styles.legendItem}>
            <Swatch fill={FILL[engine]} />
            {ENGINE_LABEL[engine]}
          </li>
        ))}
      </ul>

      <div className={styles.pair}>
        <QualityChart title={`nDCG@${k} by configuration`} rows={chartRows} metric="ndcgAtK" />
        <QualityChart title="MRR by configuration" rows={chartRows} metric="mrr" />
      </div>

      <LatencyChart rows={chartRows} />
      <p className={styles.note}>
        <Swatch fill="var(--ink)" /> solid: up to the median (p50)
        <Swatch fill="var(--ink)" opacity={0.3} /> pale: from p50 to p95. Labels read p50 / p95.
      </p>
    </div>
  );
}
