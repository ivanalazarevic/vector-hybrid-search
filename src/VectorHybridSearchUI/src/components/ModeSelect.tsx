import type { SearchMode } from "../api";
import { MODE_LABEL } from "../lib/labels";
import { MODES } from "../lib/queryState";
import { Segmented } from "./Segmented";

const OPTIONS = MODES.map((mode) => ({ value: mode, label: MODE_LABEL[mode] }));

interface ModeSelectProps {
  value: SearchMode;
  onChange: (mode: SearchMode) => void;
}

export function ModeSelect({ value, onChange }: ModeSelectProps) {
  return <Segmented legend="Mode" name="mode" value={value} options={OPTIONS} onChange={onChange} />;
}
