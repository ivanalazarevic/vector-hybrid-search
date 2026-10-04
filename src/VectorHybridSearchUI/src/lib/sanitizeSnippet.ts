const EM_TAG = /(<\/?em>)/;

// Entities the backend's HTML encoding can produce. They decode to text, never to markup,
// so they are kept as they are instead of being escaped a second time.
const ESCAPABLE = /&(?!(?:[a-zA-Z][a-zA-Z0-9]{1,31}|#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6});)|[<>"']/g;

const REPLACEMENTS: Record<string, string> = {
  "&": "&amp;",
  "<": "&lt;",
  ">": "&gt;",
  '"': "&quot;",
  "'": "&#39;",
};

function escapeText(text: string): string {
  return text.replace(ESCAPABLE, (character) => REPLACEMENTS[character]);
}

/**
 * Turns a search snippet into HTML that is safe to insert: everything is escaped, and only
 * exact `<em>` / `</em>` highlight tags survive. Highlights are kept balanced, so a stray or
 * nested tag cannot leak emphasis out of the snippet.
 */
export function sanitizeSnippet(snippet: string): string {
  let html = "";
  let highlighted = false;

  for (const part of snippet.split(EM_TAG)) {
    if (part === "<em>") {
      if (!highlighted) {
        html += "<em>";
        highlighted = true;
      }
    } else if (part === "</em>") {
      if (highlighted) {
        html += "</em>";
        highlighted = false;
      }
    } else {
      html += escapeText(part);
    }
  }

  return highlighted ? `${html}</em>` : html;
}
