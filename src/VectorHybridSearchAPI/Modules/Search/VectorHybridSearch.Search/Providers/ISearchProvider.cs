using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Providers;

public interface ISearchProvider
{
    SearchEngine Engine { get; }

    bool Supports(SearchMode mode);

    Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken);
}
