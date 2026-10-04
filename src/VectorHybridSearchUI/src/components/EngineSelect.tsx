import type { SearchEngine } from "../api";
import { ENGINE_LABEL } from "../lib/labels";
import { ENGINES } from "../lib/queryState";
import { EngineGlyph } from "./EngineBadge";
import { Segmented } from "./Segmented";

const OPTIONS = ENGINES.map((engine) => ({
  value: engine,
  engine,
  label: (
    <>
      <EngineGlyph engine={engine} />
      {ENGINE_LABEL[engine]}
    </>
  ),
}));

interface EngineSelectProps {
  value: SearchEngine;
  onChange: (engine: SearchEngine) => void;
}

export function EngineSelect({ value, onChange }: EngineSelectProps) {
  return <Segmented legend="Engine" name="engine" value={value} options={OPTIONS} onChange={onChange} />;
}
