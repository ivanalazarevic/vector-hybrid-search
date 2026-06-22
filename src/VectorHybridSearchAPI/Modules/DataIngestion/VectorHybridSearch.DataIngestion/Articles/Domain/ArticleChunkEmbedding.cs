namespace VectorHybridSearch.DataIngestion.Articles.Domain;

public sealed record ArticleChunkEmbedding(
    string Id,
    string ArticleId,
    string ChunkId,
    int ChunkIndex,
    string Model,
    int Dimensions,
    IReadOnlyCollection<float> Vector,
    DateTimeOffset GeneratedAt);
