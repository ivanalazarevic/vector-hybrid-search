using MongoDB.Bson;
using VectorHybridSearch.MongoDb.Search;

namespace VectorHybridSearch.Tests.MongoDb;

public sealed class MongoDbPipelineStagesTests
{
    [Fact]
    public void BuildArticlePipeline_CollapsesChunksToArticlesAfterTheSearchStage()
    {
        var searchStage = new BsonDocument("$search", new BsonDocument());
        var projectionStage = MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "searchScore", includeHighlights: true);

        var pipeline = MongoDbPipelineStages.BuildArticlePipeline(searchStage, projectionStage, topK: 7);

        Assert.Equal(
            ["$search", "$project", "$sort", "$group", "$replaceWith", "$sort", "$limit"],
            pipeline.Select(stage => stage.GetElement(0).Name));
        Assert.Same(searchStage, pipeline[0]);
        Assert.Same(projectionStage, pipeline[1]);
        Assert.Equal(7, pipeline[6]["$limit"].AsInt32);
    }

    [Fact]
    public void BuildProjectionStage_ProjectsTheRequestedScoreAndOptionalHighlights()
    {
        var withHighlights = MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "searchScore", includeHighlights: true)["$project"].AsBsonDocument;
        var withoutHighlights = MongoDbPipelineStages.BuildProjectionStage(scoreMeta: "vectorSearchScore", includeHighlights: false)["$project"].AsBsonDocument;

        Assert.Equal("searchScore", withHighlights["score"]["$meta"].AsString);
        Assert.Equal("searchHighlights", withHighlights["highlights"]["$meta"].AsString);

        Assert.Equal("vectorSearchScore", withoutHighlights["score"]["$meta"].AsString);
        Assert.False(withoutHighlights.Contains("highlights"));
    }

    [Fact]
    public void BuildScoreSortStage_BreaksScoreTiesOnArticleId()
    {
        var sort = MongoDbPipelineStages.BuildScoreSortStage()["$sort"].AsBsonDocument;

        Assert.Equal(["score", "articleId"], sort.Names);
        Assert.Equal(-1, sort["score"].AsInt32);
        Assert.Equal(1, sort["articleId"].AsInt32);
    }
}
