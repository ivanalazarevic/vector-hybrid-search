using System.Globalization;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Search.Fusion;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public sealed class ElasticsearchHybridSearchProvider(
    HttpClient httpClient,
    ElasticsearchOptions options,
    IEmbeddingService embeddingService,
    ElasticsearchBm25SearchProvider bm25Provider,
    ElasticsearchVectorSearchProvider vectorProvider) : ISearchProvider
{
    public SearchEngine Engine => SearchEngine.Elasticsearch;

    public bool Supports(SearchMode mode) => mode == SearchMode.Hybrid;

    public Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        return HybridSearch.GetOptions(request).Strategy == HybridStrategy.Native
            ? SearchNativeAsync(request, cancellationToken)
            : HybridSearch.RunRrfAsync(bm25Provider, vectorProvider, request, cancellationToken);
    }

    // script_score only re-scores the documents matched by the BM25 query, so an article found only by vector
    // similarity cannot appear here. The rrf retriever would fuse both lists, but it is not in the basic licence.
    private async Task<SearchProviderResult> SearchNativeAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var hybrid = HybridSearch.GetOptions(request);
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);

        var requestBody = new {
            size = request.TopK,
            query = new {
                script_score = new {
                    query = ElasticsearchQueryBuilder.BuildFilteredTextQuery(request),
                    script = new {
                        source = "(_score * params.bm25Weight) + ((cosineSimilarity(params.queryVector, 'embedding') + 1.0) * params.vectorWeight)",
                        @params = new {
                            queryVector = queryEmbedding.Vector,
                            bm25Weight = hybrid.Bm25Weight,
                            vectorWeight = hybrid.VectorWeight
                        }
                    }
                }
            },
            collapse = new {
                field = "articleId"
            },
            highlight = ElasticsearchQueryBuilder.BuildChunkTextHighlight()
        };

        using var json = await ElasticsearchHttp.PostSearchAsync(
            httpClient,
            options.IndexName,
            requestBody,
            "run Elasticsearch hybrid search",
            cancellationToken);

        var results = ElasticsearchResultParser.ParseResults(json.RootElement);

        return new SearchProviderResult(
            Results: results,
            Diagnostics: new SearchDiagnostics(
                Strategy: "Elasticsearch script_score hybrid: BM25 candidate selection plus cosine vector reranking",
                RequestedTopK: request.TopK,
                Metadata: new Dictionary<string, string> {
                    ["index"] = options.IndexName,
                    ["mode"] = nameof(SearchMode.Hybrid),
                    ["engine"] = nameof(SearchEngine.Elasticsearch),
                    ["hybridStrategy"] = nameof(HybridStrategy.Native),
                    ["nativeImplementation"] = "script_score",
                    ["embeddingModel"] = queryEmbedding.Model,
                    ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                    ["bm25Weight"] = hybrid.Bm25Weight.ToString(CultureInfo.InvariantCulture),
                    ["vectorWeight"] = hybrid.VectorWeight.ToString(CultureInfo.InvariantCulture),
                    ["rrfK"] = "not used: script_score combines scores, not ranks",
                    ["resultGrouping"] = "collapse:articleId"
                }));
    }
}
