namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchRequest(
    string Query,
    SearchEngine Engine = SearchEngine.Elasticsearch,
    SearchMode Mode = SearchMode.Hybrid,
    int TopK = 10,
    SearchFilters? Filters = null,
    bool IncludeDiagnostics = false,
    HybridOptions? Hybrid = null);
