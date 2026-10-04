using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.MongoDb.Indexing;
using VectorHybridSearch.Search.Fusion;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.MongoDb.Search;

public sealed class MongoDbHybridSearchProvider(
    IMongoClient mongoClient,
    MongoDbOptions options,
    IEmbeddingService embeddingService,
    MongoDbBm25SearchProvider bm25Provider,
    MongoDbVectorSearchProvider vectorProvider) : ISearchProvider
{
    public SearchEngine Engine => SearchEngine.MongoDbAtlas;

    public bool Supports(SearchMode mode) => mode == SearchMode.Hybrid;

    public Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        return HybridSearch.GetOptions(request).Strategy == HybridStrategy.Native
            ? SearchNativeAsync(request, cancellationToken)
            : HybridSearch.RunRrfAsync(bm25Provider, vectorProvider, request, cancellationToken);
    }

    // $rankFusion fuses chunks, not articles, and search highlights are not available after it,
    // so the snippet is the start of the best chunk as in vector search.
    private async Task<SearchProviderResult> SearchNativeAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var hybrid = HybridSearch.GetOptions(request);
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);

        var collection = mongoClient
            .GetDatabase(options.DatabaseName)
            .GetCollection<BsonDocument>(options.CollectionName);

        var candidateLimit = MongoDbQueryBuilder.GetCandidateLimit(request.TopK);
        var numCandidates = MongoDbQueryBuilder.GetNumCandidates(candidateLimit);

        var rankFusionStage = MongoDbQueryBuilder.BuildRankFusionStage(
            textSearchStage: MongoDbQueryBuilder.BuildTextSearchStage(request, options.SearchIndexName, includeHighlight: false),
            vectorSearchStage: MongoDbQueryBuilder.BuildVectorSearchStage(
                request.Filters,
                options.VectorSearchIndexName,
                queryEmbedding.Vector,
                candidateLimit,
                numCandidates),
            textCandidateLimit: candidateLimit,
            options: hybrid);

        var pipeline = MongoDbPipelineStages.BuildArticlePipeline(
            searchStage: rankFusionStage,
            projectionStage: MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "score", includeHighlights: false),
            topK: request.TopK);

        try {
            var documents = await collection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync(cancellationToken);

            return new SearchProviderResult(
                Results: MongoDbResultParser.ParseResults(documents),
                Diagnostics: new SearchDiagnostics(
                    Strategy: "MongoDB $rankFusion hybrid: reciprocal rank fusion of $search and $vectorSearch chunk results",
                    RequestedTopK: request.TopK,
                    Metadata: new Dictionary<string, string> {
                        ["database"] = options.DatabaseName,
                        ["collection"] = options.CollectionName,
                        ["bm25.index"] = options.SearchIndexName,
                        ["vector.index"] = options.VectorSearchIndexName,
                        ["mode"] = nameof(SearchMode.Hybrid),
                        ["engine"] = nameof(SearchEngine.MongoDbAtlas),
                        ["hybridStrategy"] = nameof(HybridStrategy.Native),
                        ["nativeImplementation"] = "$rankFusion",
                        ["embeddingModel"] = queryEmbedding.Model,
                        ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                        ["bm25Weight"] = hybrid.Bm25Weight.ToString(CultureInfo.InvariantCulture),
                        ["vectorWeight"] = hybrid.VectorWeight.ToString(CultureInfo.InvariantCulture),
                        ["rrfK"] = "60 (fixed by $rankFusion; the requested value is not used)",
                        ["candidateLimit"] = candidateLimit.ToString(CultureInfo.InvariantCulture),
                        ["numCandidates"] = numCandidates.ToString(CultureInfo.InvariantCulture),
                        ["resultGrouping"] = "group:articleId"
                    }));
        }
        catch (MongoException exception) {
            throw new InvalidOperationException(
                $"MongoDB $rankFusion could not query indexes '{options.SearchIndexName}' and '{options.VectorSearchIndexName}'. " +
                "Make sure both indexes exist and have reached READY status, and that the server is MongoDB 8.1 or later.",
                exception);
        }
    }
}
