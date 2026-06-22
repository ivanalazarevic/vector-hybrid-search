namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record ArticleChunkEmbeddingDto(
    string Id,
    string ArticleId,
    string ChunkId,
    int ChunkIndex,
    string Model,
    int Dimensions,
    IReadOnlyCollection<float> Vector,
    DateTimeOffset GeneratedAt);
