namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record IngestArticlesResponse(
    int Received,
    int Accepted,
    int CreatedChunks,
    int CreatedEmbeddings,
    IReadOnlyCollection<SearchIndexingResultDto> IndexingResults,
    IReadOnlyCollection<string> RejectedIds,
    string Status);
