namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record ArticleChunkDto(
    string Id,
    string ArticleId,
    int ChunkIndex,
    string Text,
    int WordCount);
