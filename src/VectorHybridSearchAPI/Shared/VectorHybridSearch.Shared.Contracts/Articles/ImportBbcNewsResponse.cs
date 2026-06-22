namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record ImportBbcNewsResponse(
    string FilePath,
    int ReadLines,
    int ImportedArticles,
    int CreatedChunks,
    int CreatedEmbeddings,
    IReadOnlyCollection<SearchIndexingResultDto> IndexingResults,
    IReadOnlyCollection<string> RejectedIds,
    string Status);
