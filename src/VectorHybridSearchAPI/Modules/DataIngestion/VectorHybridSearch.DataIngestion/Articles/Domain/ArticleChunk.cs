namespace VectorHybridSearch.DataIngestion.Articles.Domain;

public sealed record ArticleChunk(
    string Id,
    string ArticleId,
    int ChunkIndex,
    string Text,
    int WordCount);
