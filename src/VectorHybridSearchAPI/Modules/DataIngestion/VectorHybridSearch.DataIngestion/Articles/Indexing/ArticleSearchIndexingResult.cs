namespace VectorHybridSearch.DataIngestion.Articles.Indexing;

public sealed record ArticleSearchIndexingResult(
    string Source,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding);
