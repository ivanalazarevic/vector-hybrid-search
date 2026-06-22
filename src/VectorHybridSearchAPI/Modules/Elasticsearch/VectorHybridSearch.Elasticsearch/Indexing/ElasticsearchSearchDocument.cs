namespace VectorHybridSearch.Elasticsearch.Indexing;

public sealed record ElasticsearchSearchDocument(
    string Id,
    string ArticleId,
    string ChunkId,
    int ChunkIndex,
    string Title,
    string? Summary,
    string? Author,
    string? Source,
    string? Url,
    string? Category,
    DateTimeOffset? PublishedAt,
    DateTimeOffset IngestedAt,
    string ChunkText,
    int WordCount,
    string EmbeddingModel,
    int EmbeddingDimensions,
    IReadOnlyCollection<float> Embedding);
