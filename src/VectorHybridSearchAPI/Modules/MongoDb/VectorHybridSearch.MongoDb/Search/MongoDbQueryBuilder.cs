using MongoDB.Bson;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.MongoDb.Search;

public static class MongoDbQueryBuilder
{
    public const string RankFusionTextPipeline = "bm25";
    public const string RankFusionVectorPipeline = "vector";

    public static BsonDocument BuildTextSearchStage(SearchRequest request, string indexName, bool includeHighlight)
    {
        var compound = new BsonDocument {
            {
                "should", new BsonArray {
                    CreateTextQuery(request.Query, "chunkText", 3),
                    CreateTextQuery(request.Query, "title", 4),
                    CreateTextQuery(request.Query, "summary", 1),
                    CreatePhraseQuery(request.Query, "title", 8),
                    CreatePhraseQuery(request.Query, "chunkText", 5)
                }
            },
            { "minimumShouldMatch", 1 }
        };

        var filters = BuildTextSearchFilters(request.Filters);
        if (filters.Count > 0) {
            compound.Add("filter", filters);
        }

        var search = new BsonDocument {
            { "index", indexName },
            { "compound", compound }
        };

        if (includeHighlight) {
            search.Add("highlight", new BsonDocument("path", "chunkText"));
        }

        return new BsonDocument("$search", search);
    }

    public static BsonDocument BuildVectorSearchStage(
        SearchFilters? filters,
        string indexName,
        IReadOnlyCollection<float> queryVector,
        int candidateLimit,
        int numCandidates)
    {
        var vectorSearch = new BsonDocument {
            { "index", indexName },
            { "path", "embedding" },
            { "queryVector", new BsonArray(queryVector.Select(value => (BsonValue)value)) },
            { "numCandidates", numCandidates },
            { "limit", candidateLimit }
        };

        var filter = BuildVectorSearchFilter(filters);
        if (filter is not null) {
            vectorSearch.Add("filter", filter);
        }

        return new BsonDocument("$vectorSearch", vectorSearch);
    }

    // Chunk-level candidates fetched before the hits are collapsed to articles.
    public static int GetCandidateLimit(int topK) => Math.Max(topK * 10, 100);

    public static int GetNumCandidates(int candidateLimit) => Math.Min(Math.Max(candidateLimit * 20, 1000), 10000);

    // $rankFusion fuses the two chunk-level lists with reciprocal rank fusion. Its rank constant is fixed at 60.
    public static BsonDocument BuildRankFusionStage(
        BsonDocument textSearchStage,
        BsonDocument vectorSearchStage,
        int textCandidateLimit,
        HybridOptions options)
    {
        return new BsonDocument(
            "$rankFusion",
            new BsonDocument {
                {
                    "input", new BsonDocument(
                        "pipelines",
                        new BsonDocument {
                            { RankFusionTextPipeline, new BsonArray { textSearchStage, new BsonDocument("$limit", textCandidateLimit) } },
                            { RankFusionVectorPipeline, new BsonArray { vectorSearchStage } }
                        })
                },
                {
                    "combination", new BsonDocument(
                        "weights",
                        new BsonDocument {
                            { RankFusionTextPipeline, options.Bm25Weight },
                            { RankFusionVectorPipeline, options.VectorWeight }
                        })
                }
            });
    }

    private static BsonDocument CreateTextQuery(string query, string path, int boost)
    {
        return new BsonDocument(
            "text",
            new BsonDocument {
                { "query", query },
                { "path", path },
                { "score", CreateBoost(boost) }
            });
    }

    private static BsonDocument CreatePhraseQuery(string query, string path, int boost)
    {
        return new BsonDocument(
            "phrase",
            new BsonDocument {
                { "query", query },
                { "path", path },
                { "score", CreateBoost(boost) }
            });
    }

    private static BsonDocument CreateBoost(int value)
    {
        return new BsonDocument(
            "boost",
            new BsonDocument("value", value));
    }

    private static BsonArray BuildTextSearchFilters(SearchFilters? filters)
    {
        var result = new BsonArray();

        if (filters is null) {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(filters.Category)) {
            result.Add(CreateEqualsFilter("category", filters.Category));
        }

        if (!string.IsNullOrWhiteSpace(filters.Source)) {
            result.Add(CreateEqualsFilter("source", filters.Source));
        }

        if (filters.PublishedFrom is not null || filters.PublishedTo is not null) {
            var range = new BsonDocument("path", "publishedAt");

            if (filters.PublishedFrom is not null) {
                range.Add("gte", filters.PublishedFrom.Value.UtcDateTime);
            }

            if (filters.PublishedTo is not null) {
                range.Add("lte", filters.PublishedTo.Value.UtcDateTime);
            }

            result.Add(new BsonDocument("range", range));
        }

        return result;
    }

    private static BsonDocument CreateEqualsFilter(string path, string value)
    {
        return new BsonDocument(
            "equals",
            new BsonDocument {
                { "path", path },
                { "value", value }
            });
    }

    private static BsonDocument? BuildVectorSearchFilter(SearchFilters? filters)
    {
        if (filters is null) {
            return null;
        }

        var clauses = new BsonArray();

        if (!string.IsNullOrWhiteSpace(filters.Category)) {
            clauses.Add(new BsonDocument("category", filters.Category));
        }

        if (!string.IsNullOrWhiteSpace(filters.Source)) {
            clauses.Add(new BsonDocument("source", filters.Source));
        }

        if (filters.PublishedFrom is not null || filters.PublishedTo is not null) {
            var range = new BsonDocument();

            if (filters.PublishedFrom is not null) {
                range.Add("$gte", filters.PublishedFrom.Value.UtcDateTime);
            }

            if (filters.PublishedTo is not null) {
                range.Add("$lte", filters.PublishedTo.Value.UtcDateTime);
            }

            clauses.Add(new BsonDocument("publishedAt", range));
        }

        return clauses.Count switch {
            0 => null,
            1 => clauses[0].AsBsonDocument,
            _ => new BsonDocument("$and", clauses)
        };
    }
}
