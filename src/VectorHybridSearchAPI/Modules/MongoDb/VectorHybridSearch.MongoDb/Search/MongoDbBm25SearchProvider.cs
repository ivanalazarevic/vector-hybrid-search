using MongoDB.Bson;
using MongoDB.Driver;
using VectorHybridSearch.MongoDb.Indexing;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.MongoDb.Search;

public sealed class MongoDbBm25SearchProvider(
    IMongoClient mongoClient,
    MongoDbOptions options) : ISearchProvider
{
    public SearchEngine Engine => SearchEngine.MongoDbAtlas;

    public bool Supports(SearchMode mode) => mode == SearchMode.Bm25;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var collection = mongoClient
            .GetDatabase(options.DatabaseName)
            .GetCollection<BsonDocument>(options.CollectionName);

        var pipeline = MongoDbPipelineStages.BuildArticlePipeline(
            searchStage: MongoDbQueryBuilder.BuildTextSearchStage(request, options.SearchIndexName, includeHighlight: true),
            projectionStage: MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "searchScore", includeHighlights: true),
            topK: request.TopK);

        try {
            var documents = await collection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync(cancellationToken);

            return new SearchProviderResult(
                Results: MongoDbResultParser.ParseResults(documents),
                Diagnostics: new SearchDiagnostics(
                    Strategy: "MongoDB Search BM25 compound query over chunkText, title, and summary",
                    RequestedTopK: request.TopK,
                    Metadata: new Dictionary<string, string> {
                        ["database"] = options.DatabaseName,
                        ["collection"] = options.CollectionName,
                        ["index"] = options.SearchIndexName,
                        ["mode"] = nameof(SearchMode.Bm25),
                        ["engine"] = nameof(SearchEngine.MongoDbAtlas),
                        ["titleBoost"] = "4",
                        ["chunkTextBoost"] = "3",
                        ["titlePhraseBoost"] = "8",
                        ["chunkTextPhraseBoost"] = "5",
                        ["resultGrouping"] = "group:articleId"
                    }));
        }
        catch (MongoException exception) {
            throw new InvalidOperationException(
                $"MongoDB Search could not query index '{options.SearchIndexName}'. " +
                "Make sure the index exists and has reached READY status.",
                exception);
        }
    }
}
