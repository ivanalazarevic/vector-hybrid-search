using MongoDB.Bson.Serialization.Attributes;

namespace VectorHybridSearch.MongoDb.Indexing;

public sealed record MongoDbSearchDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("articleId")]
    public required string ArticleId { get; init; }

    [BsonElement("chunkId")]
    public required string ChunkId { get; init; }

    [BsonElement("chunkIndex")]
    public required int ChunkIndex { get; init; }

    [BsonElement("title")]
    public required string Title { get; init; }

    [BsonElement("summary")]
    public string? Summary { get; init; }

    [BsonElement("author")]
    public string? Author { get; init; }

    [BsonElement("source")]
    public string? Source { get; init; }

    [BsonElement("url")]
    public string? Url { get; init; }

    [BsonElement("category")]
    public string? Category { get; init; }

    [BsonElement("publishedAt")]
    public DateTime? PublishedAt { get; init; }

    [BsonElement("ingestedAt")]
    public required DateTime IngestedAt { get; init; }

    [BsonElement("chunkText")]
    public required string ChunkText { get; init; }

    [BsonElement("wordCount")]
    public required int WordCount { get; init; }

    [BsonElement("embeddingModel")]
    public required string EmbeddingModel { get; init; }

    [BsonElement("embeddingDimensions")]
    public required int EmbeddingDimensions { get; init; }

    [BsonElement("embedding")]
    public required float[] Embedding { get; init; }
}
