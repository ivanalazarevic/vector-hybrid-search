using MongoDB.Bson;

namespace VectorHybridSearch.MongoDb.Search;

public static class MongoDbPipelineStages
{
    // Chunk-level search stage followed by the stages that collapse the hits to one best chunk per article.
    public static BsonDocument[] BuildArticlePipeline(
        BsonDocument searchStage,
        BsonDocument projectionStage,
        int topK)
    {
        return [
            searchStage,
            projectionStage,
            BuildScoreSortStage(),
            BuildArticleGroupingStage(),
            BuildReplaceRootStage(),
            BuildScoreSortStage(),
            new BsonDocument("$limit", topK)
        ];
    }

    public static BsonDocument BuildProjectionStage(string scoreMeta, bool includeHighlights)
    {
        var projection = new BsonDocument {
            { "_id", 0 },
            { "articleId", 1 },
            { "title", 1 },
            { "chunkText", 1 },
            { "source", 1 },
            { "category", 1 },
            { "score", new BsonDocument("$meta", scoreMeta) }
        };

        if (includeHighlights) {
            projection.Add("highlights", new BsonDocument("$meta", "searchHighlights"));
        }

        return new BsonDocument("$project", projection);
    }

    public static BsonDocument BuildScoreSortStage()
    {
        return new BsonDocument(
            "$sort",
            new BsonDocument {
                { "score", -1 },
                { "articleId", 1 }
            });
    }

    public static BsonDocument BuildArticleGroupingStage()
    {
        return new BsonDocument(
            "$group",
            new BsonDocument {
                { "_id", "$articleId" },
                { "bestChunk", new BsonDocument("$first", "$$ROOT") }
            });
    }

    public static BsonDocument BuildReplaceRootStage()
    {
        return new BsonDocument(
            "$replaceWith",
            "$bestChunk");
    }
}
