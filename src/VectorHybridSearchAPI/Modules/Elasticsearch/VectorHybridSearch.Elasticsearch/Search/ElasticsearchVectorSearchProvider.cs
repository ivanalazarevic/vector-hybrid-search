using System.Globalization;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public sealed class ElasticsearchVectorSearchProvider(
    HttpClient httpClient,
    ElasticsearchOptions options,
    IEmbeddingService embeddingService) : ISearchProvider
{
    private const int MaxNumCandidates = 10000;

    public SearchEngine Engine => SearchEngine.Elasticsearch;

    public bool Supports(SearchMode mode) => mode == SearchMode.Vector;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);
        var filters = ElasticsearchQueryBuilder.BuildFilters(request.Filters);
        // Elasticsearch rejects num_candidates above 10000, and k may not exceed num_candidates.
        var candidateK = Math.Min(Math.Max(request.TopK * 10, 100), MaxNumCandidates);
        var numCandidates = Math.Min(Math.Max(candidateK * 10, 1000), MaxNumCandidates);

        object knn = filters.Count == 0
            ? new {
                field = "embedding",
                query_vector = queryEmbedding.Vector,
                k = candidateK,
                num_candidates = numCandidates
            }
            : new {
                field = "embedding",
                query_vector = queryEmbedding.Vector,
                k = candidateK,
                num_candidates = numCandidates,
                filter = filters
            };

        var requestBody = new {
            knn,
            size = request.TopK,
            collapse = new {
                field = "articleId"
            },
            _source = new[] {
                "articleId",
                "title",
                "chunkText",
                "source",
                "category"
            }
        };

        using var json = await ElasticsearchHttp.PostSearchAsync(
            httpClient,
            options.IndexName,
            requestBody,
            "run Elasticsearch vector search",
            cancellationToken);

        var results = ElasticsearchResultParser.ParseResults(json.RootElement);

        return new SearchProviderResult(
            Results: results,
            Diagnostics: new SearchDiagnostics(
                Strategy: "Elasticsearch kNN vector search over embedding field",
                RequestedTopK: request.TopK,
                Metadata: new Dictionary<string, string> {
                    ["index"] = options.IndexName,
                    ["mode"] = nameof(SearchMode.Vector),
                    ["engine"] = nameof(SearchEngine.Elasticsearch),
                    ["embeddingModel"] = queryEmbedding.Model,
                    ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                    ["candidateK"] = candidateK.ToString(CultureInfo.InvariantCulture),
                    ["numCandidates"] = numCandidates.ToString(CultureInfo.InvariantCulture),
                    ["resultGrouping"] = "collapse:articleId"
                }));
    }
}
