import type { ReactNode } from "react";
import type { SearchEngine } from "../api";
import styles from "./Segmented.module.css";

export interface SegmentedOption<T extends string> {
  value: T;
  label: ReactNode;
  /** Colours the selected state with this engine's colour instead of ink. */
  engine?: SearchEngine;
}

interface SegmentedProps<T extends string> {
  legend: string;
  name: string;
  value: T;
  options: readonly SegmentedOption<T>[];
  onChange: (value: T) => void;
}

/** A radio group drawn as one row of buttons: every option stays visible, which reads better in screenshots than a closed select. */
export function Segmented<T extends string>({ legend, name, value, options, onChange }: SegmentedProps<T>) {
  return (
    <fieldset className={styles.group}>
      <legend className={styles.legend}>{legend}</legend>
      <div className={styles.track}>
        {options.map((option) => (
          <label key={option.value} className={styles.option} data-engine={option.engine}>
            <input
              className="visually-hidden"
              type="radio"
              name={name}
              value={option.value}
              checked={option.value === value}
              onChange={() => onChange(option.value)}
            />
            <span className={styles.face}>{option.label}</span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
