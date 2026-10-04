import type { ApiClient } from "./client";
import { mockCompare } from "./mocks/compare";
import { mockGetRun, mockListRuns } from "./mocks/evaluation";
import { mockSearch } from "./mocks/search";
import { mockStorage } from "./mocks/storage";

const DELAY_MS = 280;

/** Waits like a network call would, then produces the value (or throws the mock's ApiError). */
function later<T>(produce: () => T, signal?: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const abort = () => {
      clearTimeout(timer);
      reject(new DOMException("The request was aborted.", "AbortError"));
    };
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", abort);
      try {
        resolve(produce());
      } catch (error) {
        reject(error);
      }
    }, DELAY_MS);
    if (signal?.aborted) {
      abort();
    } else {
      signal?.addEventListener("abort", abort, { once: true });
    }
  });
}

export const mockClient: ApiClient = {
  search: (request, signal) => later(() => mockSearch(request), signal),
  compare: (request, signal) => later(() => mockCompare(request), signal),
  listRuns: (signal) => later(mockListRuns, signal),
  getRun: (runId, signal) => later(() => mockGetRun(runId), signal),
  getStorage: (signal) => later(mockStorage, signal),
};
