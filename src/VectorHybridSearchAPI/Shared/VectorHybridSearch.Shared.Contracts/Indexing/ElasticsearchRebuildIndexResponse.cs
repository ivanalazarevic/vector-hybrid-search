namespace VectorHybridSearch.Shared.Contracts.Indexing;

public sealed record ElasticsearchRebuildIndexResponse(
    string IndexName,
    int Articles,
    int Chunks,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding,
    string Status);
