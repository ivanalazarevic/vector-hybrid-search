import { sanitizeSnippet } from "../lib/sanitizeSnippet";
import styles from "./Snippet.module.css";

/** The only place snippet HTML reaches the DOM. sanitizeSnippet leaves nothing but text and <em>. */
export function Snippet({ html }: { html: string }) {
  return <p className={styles.snippet} dangerouslySetInnerHTML={{ __html: sanitizeSnippet(html) }} />;
}
