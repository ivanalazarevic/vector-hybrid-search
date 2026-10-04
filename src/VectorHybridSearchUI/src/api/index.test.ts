import { describe, expect, it, vi } from "vitest";
import type { ApiClient } from "./client";
import { createApi, parseMocks } from "./index";

function fakeClient(): ApiClient {
  return {
    search: vi.fn(),
    compare: vi.fn(),
    listRuns: vi.fn(),
    getRun: vi.fn(),
    getStorage: vi.fn(),
  };
}

const searchRequest = {
  query: "rates",
  engine: "Elasticsearch",
  mode: "Bm25",
  topK: 10,
  includeDiagnostics: false,
} as const;

describe("parseMocks", () => {
  it("reads a comma-separated list", () => {
    expect([...parseMocks("search,compare,analytics")]).toEqual(["search", "compare", "analytics"]);
  });

  it("treats an empty or missing value as nothing mocked", () => {
    expect(parseMocks("").size).toBe(0);
    expect(parseMocks(undefined).size).toBe(0);
  });

  it("ignores spaces, casing and unknown names", () => {
    expect([...parseMocks(" Compare , charts ")]).toEqual(["compare"]);
  });
});

describe("createApi", () => {
  it("sends a mocked feature to the mock client and the rest to the http client", () => {
    const mock = fakeClient();
    const http = fakeClient();
    const api = createApi(parseMocks("search"), mock, http);

    void api.search(searchRequest);
    void api.compare(searchRequest);

    expect(mock.search).toHaveBeenCalledWith(searchRequest, undefined);
    expect(http.search).not.toHaveBeenCalled();
    expect(http.compare).toHaveBeenCalledWith(searchRequest, undefined);
    expect(mock.compare).not.toHaveBeenCalled();
  });

  it("switches all three analytics calls together", () => {
    const mock = fakeClient();
    const http = fakeClient();
    const api = createApi(parseMocks("analytics"), mock, http);

    void api.listRuns();
    void api.getRun("run-1");
    void api.getStorage();

    expect(mock.listRuns).toHaveBeenCalledOnce();
    expect(mock.getRun).toHaveBeenCalledWith("run-1", undefined);
    expect(mock.getStorage).toHaveBeenCalledOnce();
    expect(http.listRuns).not.toHaveBeenCalled();
  });

  it("uses the http client for everything when nothing is mocked", () => {
    const mock = fakeClient();
    const http = fakeClient();
    const api = createApi(parseMocks(""), mock, http);

    void api.search(searchRequest);
    void api.listRuns();

    expect(http.search).toHaveBeenCalledOnce();
    expect(http.listRuns).toHaveBeenCalledOnce();
    expect(mock.search).not.toHaveBeenCalled();
    expect(mock.listRuns).not.toHaveBeenCalled();
  });
});
