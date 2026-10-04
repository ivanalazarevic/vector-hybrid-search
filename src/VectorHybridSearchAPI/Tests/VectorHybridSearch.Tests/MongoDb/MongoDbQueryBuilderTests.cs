using MongoDB.Bson;
using VectorHybridSearch.MongoDb.Search;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Tests.MongoDb;

public sealed class MongoDbQueryBuilderTests
{
    [Fact]
    public void BuildTextSearchStage_UsesTheBoostsSharedWithElasticsearch()
    {
        var search = MongoDbQueryBuilder.BuildTextSearchStage(new SearchRequest(Query: "prime minister"), "text-index", includeHighlight: true)["$search"];

        var should = search["compound"]["should"].AsBsonArray;

        Assert.Equal("text-index", search["index"].AsString);
        Assert.Equal(
            ["text:chunkText:3", "text:title:4", "text:summary:1", "phrase:title:8", "phrase:chunkText:5"],
            should.Select(clause => {
                var element = clause.AsBsonDocument.GetElement(0);
                return $"{element.Name}:{element.Value["path"].AsString}:{element.Value["score"]["boost"]["value"].AsInt32}";
            }));
        Assert.Equal(1, search["compound"]["minimumShouldMatch"].AsInt32);
        Assert.False(search["compound"].AsBsonDocument.Contains("filter"));
        Assert.Equal("chunkText", search["highlight"]["path"].AsString);
    }

    [Fact]
    public void BuildTextSearchStage_WithoutHighlight_OmitsTheHighlightBlock()
    {
        var search = MongoDbQueryBuilder.BuildTextSearchStage(new SearchRequest(Query: "football"), "text-index", includeHighlight: false)["$search"];

        Assert.False(search.AsBsonDocument.Contains("highlight"));
    }

    [Fact]
    public void BuildTextSearchStage_MapsEveryFilterToItsField()
    {
        var request = new SearchRequest(Query: "football", Filters: AllFilters());

        var filters = MongoDbQueryBuilder.BuildTextSearchStage(request, "text-index", includeHighlight: true)["$search"]["compound"]["filter"].AsBsonArray;

        Assert.Equal(3, filters.Count);
        Assert.Equal(("category", "tech"), (filters[0]["equals"]["path"].AsString, filters[0]["equals"]["value"].AsString));
        Assert.Equal(("source", "BBC"), (filters[1]["equals"]["path"].AsString, filters[1]["equals"]["value"].AsString));
        Assert.Equal("publishedAt", filters[2]["range"]["path"].AsString);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), filters[2]["range"]["gte"].ToUniversalTime());
        Assert.Equal(new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc), filters[2]["range"]["lte"].ToUniversalTime());
    }

    [Fact]
    public void BuildVectorSearchStage_WithoutFilters_HasNoFilter()
    {
        var vectorSearch = MongoDbQueryBuilder.BuildVectorSearchStage(
            filters: null,
            indexName: "vector-index",
            queryVector: [0.5f, -0.5f],
            candidateLimit: 100,
            numCandidates: 2000)["$vectorSearch"];

        Assert.Equal("vector-index", vectorSearch["index"].AsString);
        Assert.Equal("embedding", vectorSearch["path"].AsString);
        Assert.Equal([0.5d, -0.5d], vectorSearch["queryVector"].AsBsonArray.Select(value => value.ToDouble()));
        Assert.Equal(100, vectorSearch["limit"].AsInt32);
        Assert.Equal(2000, vectorSearch["numCandidates"].AsInt32);
        Assert.False(vectorSearch.AsBsonDocument.Contains("filter"));
    }

    [Fact]
    public void BuildVectorSearchStage_CombinesSeveralFiltersWithAnd()
    {
        var single = MongoDbQueryBuilder.BuildVectorSearchStage(new SearchFilters(Category: "tech"), "vector-index", [1f], 100, 2000)["$vectorSearch"];
        var several = MongoDbQueryBuilder.BuildVectorSearchStage(AllFilters(), "vector-index", [1f], 100, 2000)["$vectorSearch"];

        Assert.Equal("tech", single["filter"]["category"].AsString);

        var clauses = several["filter"]["$and"].AsBsonArray;
        Assert.Equal(3, clauses.Count);
        Assert.Equal("tech", clauses[0]["category"].AsString);
        Assert.Equal("BBC", clauses[1]["source"].AsString);
        Assert.Equal(["$gte", "$lte"], clauses[2]["publishedAt"].AsBsonDocument.Names);
    }

    [Theory]
    [InlineData(1, 100, 2000)]
    [InlineData(10, 100, 2000)]
    [InlineData(50, 500, 10000)]
    [InlineData(500, 5000, 10000)]
    public void CandidateLimits_GrowWithTopKAndStayWithinTheVectorSearchMaximum(int topK, int expectedLimit, int expectedNumCandidates)
    {
        var candidateLimit = MongoDbQueryBuilder.GetCandidateLimit(topK);

        Assert.Equal(expectedLimit, candidateLimit);
        Assert.Equal(expectedNumCandidates, MongoDbQueryBuilder.GetNumCandidates(candidateLimit));
    }

    [Fact]
    public void BuildRankFusionStage_FusesTheLimitedTextPipelineAndTheVectorPipelineWithTheRequestedWeights()
    {
        var textSearchStage = new BsonDocument("$search", new BsonDocument());
        var vectorSearchStage = new BsonDocument("$vectorSearch", new BsonDocument());

        var rankFusion = MongoDbQueryBuilder.BuildRankFusionStage(
            textSearchStage,
            vectorSearchStage,
            textCandidateLimit: 100,
            options: new HybridOptions(HybridStrategy.Native, Bm25Weight: 0.3, VectorWeight: 0.7))["$rankFusion"];

        var pipelines = rankFusion["input"]["pipelines"].AsBsonDocument;
        Assert.Equal(["bm25", "vector"], pipelines.Names);
        Assert.Equal(["$search", "$limit"], pipelines["bm25"].AsBsonArray.Select(stage => stage.AsBsonDocument.GetElement(0).Name));
        Assert.Equal(100, pipelines["bm25"][1]["$limit"].AsInt32);
        Assert.Equal(["$vectorSearch"], pipelines["vector"].AsBsonArray.Select(stage => stage.AsBsonDocument.GetElement(0).Name));

        var weights = rankFusion["combination"]["weights"];
        Assert.Equal(0.3, weights["bm25"].AsDouble);
        Assert.Equal(0.7, weights["vector"].AsDouble);
    }

    private static SearchFilters AllFilters()
    {
        return new SearchFilters(
            Category: "tech",
            Source: "BBC",
            PublishedFrom: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            PublishedTo: new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));
    }
}
