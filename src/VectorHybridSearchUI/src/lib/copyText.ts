/** Copies text to the clipboard. Resolves to false when the browser refuses both ways of doing it. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    // The async clipboard API needs a secure, focused context; the old command works in more places.
  }

  const holder = document.createElement("textarea");
  holder.value = text;
  holder.setAttribute("readonly", "");
  holder.style.position = "fixed";
  holder.style.opacity = "0";
  document.body.appendChild(holder);
  holder.select();
  try {
    return document.execCommand("copy");
  } catch {
    return false;
  } finally {
    holder.remove();
  }
}
