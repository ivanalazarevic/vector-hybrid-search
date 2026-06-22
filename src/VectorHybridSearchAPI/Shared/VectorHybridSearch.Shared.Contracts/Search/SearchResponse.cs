namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchResponse(
    string QueryId,
    string Query,
    SearchEngine Engine,
    SearchMode Mode,
    long ElapsedMs,
    IReadOnlyCollection<SearchResultDto> Results,
    SearchDiagnostics? Diagnostics = null);
