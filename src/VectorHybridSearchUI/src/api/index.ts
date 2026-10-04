import type { ApiClient } from "./client";
import { httpClient } from "./httpClient";
import { mockClient } from "./mockClient";
import { COMPARE_EXAMPLES, SEARCH_EXAMPLES, type MockExample } from "./mocks/examples";

export type Feature = "search" | "compare" | "analytics";

const FEATURES: readonly Feature[] = ["search", "compare", "analytics"];

/** Parses VITE_MOCKS ("search,compare,analytics"). Unknown names are ignored; empty or missing means nothing is mocked. */
export function parseMocks(value: string | undefined): ReadonlySet<Feature> {
  const names = (value ?? "").split(",").map((name) => name.trim().toLowerCase());
  return new Set(FEATURES.filter((feature) => names.includes(feature)));
}

/** Builds one client that routes each feature to the mock or the real implementation. */
export function createApi(mocked: ReadonlySet<Feature>, mock: ApiClient, http: ApiClient): ApiClient {
  const pick = (feature: Feature) => (mocked.has(feature) ? mock : http);
  return {
    search: (request, signal) => pick("search").search(request, signal),
    compare: (request, signal) => pick("compare").compare(request, signal),
    listRuns: (signal) => pick("analytics").listRuns(signal),
    getRun: (runId, signal) => pick("analytics").getRun(runId, signal),
    getStorage: (signal) => pick("analytics").getStorage(signal),
  };
}

const mockedFeatures = parseMocks(import.meta.env.VITE_MOCKS);
const api = createApi(mockedFeatures, mockClient, httpClient);

export const { search, compare, listRuns, getRun, getStorage } = api;

export function isMocked(feature: Feature): boolean {
  return mockedFeatures.has(feature);
}

export function mockedFeatureNames(): Feature[] {
  return FEATURES.filter((feature) => mockedFeatures.has(feature));
}

/** Example queries that reach each prepared mock case. Empty when the feature talks to the real backend. */
export function mockExamples(feature: "search" | "compare"): readonly MockExample[] {
  if (!mockedFeatures.has(feature)) {
    return [];
  }
  return feature === "search" ? SEARCH_EXAMPLES : COMPARE_EXAMPLES;
}

export { isApiError, toApiError } from "./client";
export type { ApiClient } from "./client";
export type { MockExample } from "./mocks/examples";
export type * from "./types";
