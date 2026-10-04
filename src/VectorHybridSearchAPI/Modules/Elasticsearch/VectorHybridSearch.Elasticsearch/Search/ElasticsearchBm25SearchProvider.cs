using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public sealed class ElasticsearchBm25SearchProvider(
    HttpClient httpClient,
    ElasticsearchOptions options) : ISearchProvider
{
    public SearchEngine Engine => SearchEngine.Elasticsearch;

    public bool Supports(SearchMode mode) => mode == SearchMode.Bm25;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var requestBody = new {
            size = request.TopK,
            query = ElasticsearchQueryBuilder.BuildFilteredTextQuery(request),
            collapse = new {
                field = "articleId"
            },
            highlight = ElasticsearchQueryBuilder.BuildChunkTextHighlight()
        };

        using var json = await ElasticsearchHttp.PostSearchAsync(
            httpClient,
            options.IndexName,
            requestBody,
            "run Elasticsearch BM25 search",
            cancellationToken);

        var results = ElasticsearchResultParser.ParseResults(json.RootElement);

        return new SearchProviderResult(
            Results: results,
            Diagnostics: new SearchDiagnostics(
                Strategy: "Elasticsearch BM25 multi_match over chunkText, title, and summary",
                RequestedTopK: request.TopK,
                Metadata: new Dictionary<string, string> {
                    ["index"] = options.IndexName,
                    ["mode"] = nameof(SearchMode.Bm25),
                    ["engine"] = nameof(SearchEngine.Elasticsearch),
                    ["resultGrouping"] = "collapse:articleId"
                }));
    }
}
