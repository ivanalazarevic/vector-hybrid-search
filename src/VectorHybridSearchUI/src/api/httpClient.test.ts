import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "./httpClient";

const searchRequest = {
  query: "rates",
  engine: "Elasticsearch",
  mode: "Bm25",
  topK: 10,
  includeDiagnostics: true,
} as const;

function respondWith(status: number, body: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(typeof body === "string" ? body : JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("httpClient", () => {
  it("posts the search request as JSON and returns the body", async () => {
    const fetchMock = respondWith(200, { queryId: "q1", results: [] });

    const response = await httpClient.search(searchRequest);

    expect(response).toEqual({ queryId: "q1", results: [] });
    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/api/search");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual(searchRequest);
  });

  it("maps the backend's { error } validation body to an ApiError", async () => {
    respondWith(400, { error: "Query is required." });

    await expect(httpClient.search(searchRequest)).rejects.toEqual({ status: 400, title: "Query is required." });
  });

  it("maps ProblemDetails to an ApiError", async () => {
    respondWith(502, { title: "Search engine error", detail: "all shards failed", status: 502 });

    await expect(httpClient.search(searchRequest)).rejects.toEqual({
      status: 502,
      title: "Search engine error",
      detail: "all shards failed",
    });
  });

  it("falls back to a default title when the error body is not JSON", async () => {
    respondWith(503, "upstream unavailable");

    await expect(httpClient.listRuns()).rejects.toEqual({
      status: 503,
      title: "The search engine is not reachable",
    });
  });

  it("reports status 0 when the API does not answer at all", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    await expect(httpClient.getStorage()).rejects.toMatchObject({ status: 0, title: "The API is not reachable" });
  });

  it("encodes the run id in the path", async () => {
    const fetchMock = respondWith(200, { runId: "a/b", rows: [] });

    await httpClient.getRun("a/b");

    expect(fetchMock.mock.calls[0][0]).toBe("/api/evaluation/runs/a%2Fb");
  });
});
