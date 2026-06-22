using VectorHybridSearch.DataIngestion.Articles.Indexing;

namespace VectorHybridSearch.DataIngestion.Articles.Results;

public sealed record ArticleIngestionResult(
    int Received,
    int Accepted,
    int CreatedChunks,
    int CreatedEmbeddings,
    IReadOnlyCollection<ArticleSearchIndexingResult> IndexingResults,
    IReadOnlyCollection<string> RejectedIds);
