namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchResultDto(
    string ArticleId,
    string Title,
    string Snippet,
    double Score,
    int Rank,
    string? Source,
    string? Category);
