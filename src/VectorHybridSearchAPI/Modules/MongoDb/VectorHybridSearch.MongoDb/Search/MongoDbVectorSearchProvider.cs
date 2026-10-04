using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.MongoDb.Indexing;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.MongoDb.Search;

public sealed class MongoDbVectorSearchProvider(
    IMongoClient mongoClient,
    MongoDbOptions options,
    IEmbeddingService embeddingService) : ISearchProvider
{
    public SearchEngine Engine => SearchEngine.MongoDbAtlas;

    public bool Supports(SearchMode mode) => mode == SearchMode.Vector;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);

        var collection = mongoClient
            .GetDatabase(options.DatabaseName)
            .GetCollection<BsonDocument>(options.CollectionName);

        var candidateLimit = MongoDbQueryBuilder.GetCandidateLimit(request.TopK);
        var numCandidates = MongoDbQueryBuilder.GetNumCandidates(candidateLimit);
        var pipeline = MongoDbPipelineStages.BuildArticlePipeline(
            searchStage: MongoDbQueryBuilder.BuildVectorSearchStage(
                request.Filters,
                options.VectorSearchIndexName,
                queryEmbedding.Vector,
                candidateLimit,
                numCandidates),
            projectionStage: MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "vectorSearchScore", includeHighlights: false),
            topK: request.TopK);

        try {
            var documents = await collection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync(cancellationToken);

            return new SearchProviderResult(
                Results: MongoDbResultParser.ParseResults(documents),
                Diagnostics: new SearchDiagnostics(
                    Strategy: "MongoDB $vectorSearch over embedding with article-level grouping",
                    RequestedTopK: request.TopK,
                    Metadata: new Dictionary<string, string> {
                        ["database"] = options.DatabaseName,
                        ["collection"] = options.CollectionName,
                        ["index"] = options.VectorSearchIndexName,
                        ["mode"] = nameof(SearchMode.Vector),
                        ["engine"] = nameof(SearchEngine.MongoDbAtlas),
                        ["embeddingModel"] = queryEmbedding.Model,
                        ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                        ["candidateLimit"] = candidateLimit.ToString(CultureInfo.InvariantCulture),
                        ["numCandidates"] = numCandidates.ToString(CultureInfo.InvariantCulture),
                        ["similarity"] = "cosine",
                        ["resultGrouping"] = "group:articleId"
                    }));
        }
        catch (MongoException exception) {
            throw new InvalidOperationException(
                $"MongoDB vector search could not query index '{options.VectorSearchIndexName}'. " +
                "Make sure the index exists and has reached READY status.",
                exception);
        }
    }
}
