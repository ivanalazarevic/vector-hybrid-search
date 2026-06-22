namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchFilters(
    string? Category = null,
    string? Source = null,
    DateTimeOffset? PublishedFrom = null,
    DateTimeOffset? PublishedTo = null);
