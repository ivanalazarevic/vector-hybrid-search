namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record SearchIndexingResultDto(
    string Source,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding);
