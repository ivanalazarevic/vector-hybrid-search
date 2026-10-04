import type { EngineStorageStats } from "../types";

// One document per chunk in both engines, so the counts match.
export function mockStorage(): EngineStorageStats[] {
  return [
    { engine: "Elasticsearch", documentCount: 2412, storageBytes: 10_276_045, indexingElapsedMs: 4210 },
    { engine: "MongoDbAtlas", documentCount: 2412, storageBytes: 14_785_331, indexingElapsedMs: 6875 },
  ];
}
