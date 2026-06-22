using VectorHybridSearch.DataIngestion.Articles.Indexing;

namespace VectorHybridSearch.DataIngestion.Articles.Results;

public sealed record BbcNewsImportResult(
    string FilePath,
    int ReadLines,
    int ImportedArticles,
    int CreatedChunks,
    int CreatedEmbeddings,
    IReadOnlyCollection<ArticleSearchIndexingResult> IndexingResults,
    IReadOnlyCollection<string> RejectedIds);
