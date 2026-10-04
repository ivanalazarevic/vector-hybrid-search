import { describe, expect, it } from "vitest";
import { sanitizeSnippet } from "./sanitizeSnippet";

describe("sanitizeSnippet", () => {
  it("keeps <em> highlights", () => {
    expect(sanitizeSnippet("kept <em>interest</em> <em>rates</em> on hold")).toBe(
      "kept <em>interest</em> <em>rates</em> on hold",
    );
  });

  it("escapes every other tag", () => {
    expect(sanitizeSnippet('<script>alert(1)</script><img src=x onerror="alert(1)">')).toBe(
      "&lt;script&gt;alert(1)&lt;/script&gt;&lt;img src=x onerror=&quot;alert(1)&quot;&gt;",
    );
  });

  it("escapes raw &, <, > and quotes in text", () => {
    expect(sanitizeSnippet(`R&D up from <15%, "mergers" & 'deals' > before`)).toBe(
      "R&amp;D up from &lt;15%, &quot;mergers&quot; &amp; &#39;deals&#39; &gt; before",
    );
  });

  it("does not escape text the backend already escaped", () => {
    expect(sanitizeSnippet("<em>R</em>&amp;<em>D</em> up from &lt;15% &#39;now&#39; &#x27;then&#x27;")).toBe(
      "<em>R</em>&amp;<em>D</em> up from &lt;15% &#39;now&#39; &#x27;then&#x27;",
    );
  });

  it("leaves an escaped <em> from the article text as text", () => {
    expect(sanitizeSnippet("the &lt;em&gt; tag")).toBe("the &lt;em&gt; tag");
  });

  it("rejects em tags with attributes or different casing", () => {
    expect(sanitizeSnippet('<em onclick="x()">a</em> <EM>b</EM>')).toBe(
      "&lt;em onclick=&quot;x()&quot;&gt;a &lt;EM&gt;b&lt;/EM&gt;",
    );
  });

  it("closes a highlight that was cut off", () => {
    expect(sanitizeSnippet("rates <em>rise")).toBe("rates <em>rise</em>");
  });

  it("drops a stray closing tag and flattens nested highlights", () => {
    expect(sanitizeSnippet("a</em> <em>b <em>c</em> d</em>")).toBe("a <em>b c</em> d");
  });

  it("returns an empty string unchanged", () => {
    expect(sanitizeSnippet("")).toBe("");
  });
});
