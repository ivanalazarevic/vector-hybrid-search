using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Providers;

public sealed record SearchProviderResult(
    IReadOnlyCollection<SearchResultDto> Results,
    SearchDiagnostics Diagnostics);
