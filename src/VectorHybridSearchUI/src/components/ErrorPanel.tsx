import type { ApiError } from "../api";
import controls from "./controls.module.css";
import styles from "./ErrorPanel.module.css";

const NEXT_STEP: Record<number, string> = {
  0: "Start the API (dotnet run on the Bootstrapper project), or list this feature in VITE_MOCKS to use mock data.",
  400: "Change the query or its options, then search again.",
  502: "The engine answered but could not run the query. Right after ingestion its search index may still be building; try again in a moment.",
  503: "The engine did not answer. Start the containers with docker compose up -d, then try again.",
};

interface ErrorPanelProps {
  /** `status` is absent when the failure was reported inside a successful response (one engine of a comparison). */
  error: Pick<ApiError, "title" | "detail"> & { status?: number };
  onRetry?: () => void;
}

export function ErrorPanel({ error, onRetry }: ErrorPanelProps) {
  const nextStep = error.status === undefined ? undefined : NEXT_STEP[error.status];
  return (
    <div className={styles.panel} role="alert">
      <div className={styles.head}>
        {!!error.status && <span className={styles.status}>{error.status}</span>}
        <h2 className={styles.title}>{error.title}</h2>
      </div>
      {error.detail && <pre className={styles.detail}>{error.detail}</pre>}
      {nextStep && <p className={styles.nextStep}>{nextStep}</p>}
      {onRetry && (
        <button type="button" className={controls.buttonQuiet} onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}
