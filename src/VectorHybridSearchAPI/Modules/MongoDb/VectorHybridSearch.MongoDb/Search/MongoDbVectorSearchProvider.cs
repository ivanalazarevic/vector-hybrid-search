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

        if (queryEmbedding.Dimensions != options.VectorDimensions) {
            throw new InvalidOperationException(
                $"Embedding model '{queryEmbedding.Model}' returned {queryEmbedding.Dimensions} dimensions, " +
                $"but MongoDB vector index '{options.VectorSearchIndexName}' expects {options.VectorDimensions}.");
        }

        var collection = mongoClient
            .GetDatabase(options.DatabaseName)
            .GetCollection<BsonDocument>(options.CollectionName);

        var candidateLimit = Math.Max(request.TopK * 10, 100);
        var numCandidates = Math.Min(Math.Max(candidateLimit * 20, 1000), 10000);
        var pipeline = new[] {
            BuildVectorSearchStage(request, queryEmbedding.Vector, candidateLimit, numCandidates),
            BuildProjectionStage(),
            BuildScoreSortStage(),
            BuildArticleGroupingStage(),
            new BsonDocument("$replaceWith", "$bestChunk"),
            BuildScoreSortStage(),
            new BsonDocument("$limit", request.TopK)
        };

        try {
            var documents = await collection
                .Aggregate<BsonDocument>(pipeline)
                .ToListAsync(cancellationToken);

            return new SearchProviderResult(
                Results: ParseResults(documents),
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

    private BsonDocument BuildVectorSearchStage(
        SearchRequest request,
        IReadOnlyCollection<float> queryVector,
        int candidateLimit,
        int numCandidates)
    {
        var vectorSearch = new BsonDocument {
            { "index", options.VectorSearchIndexName },
            { "path", "embedding" },
            { "queryVector", new BsonArray(queryVector.Select(value => (BsonValue)value)) },
            { "numCandidates", numCandidates },
            { "limit", candidateLimit }
        };

        var filter = BuildFilter(request.Filters);
        if (filter is not null) {
            vectorSearch.Add("filter", filter);
        }

        return new BsonDocument("$vectorSearch", vectorSearch);
    }

    private static BsonDocument BuildProjectionStage()
    {
        return new BsonDocument(
            "$project",
            new BsonDocument {
                { "_id", 0 },
                { "articleId", 1 },
                { "title", 1 },
                { "chunkText", 1 },
                { "source", 1 },
                { "category", 1 },
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            });
    }

    private static BsonDocument BuildScoreSortStage()
    {
        return new BsonDocument(
            "$sort",
            new BsonDocument {
                { "score", -1 },
                { "articleId", 1 }
            });
    }

    private static BsonDocument BuildArticleGroupingStage()
    {
        return new BsonDocument(
            "$group",
            new BsonDocument {
                { "_id", "$articleId" },
                { "bestChunk", new BsonDocument("$first", "$$ROOT") }
            });
    }

    private static BsonDocument? BuildFilter(SearchFilters? filters)
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

    private static IReadOnlyCollection<SearchResultDto> ParseResults(
        IReadOnlyCollection<BsonDocument> documents)
    {
        var results = new List<SearchResultDto>(documents.Count);
        var rank = 1;

        foreach (var document in documents) {
            results.Add(new SearchResultDto(
                ArticleId: GetString(document, "articleId"),
                Title: GetString(document, "title"),
                Snippet: GetSnippet(document),
                Score: document.GetValue("score", 0d).ToDouble(),
                Rank: rank,
                Source: GetOptionalString(document, "source"),
                Category: GetOptionalString(document, "category")));

            rank++;
        }

        return results;
    }

    private static string GetSnippet(BsonDocument document)
    {
        var chunkText = GetString(document, "chunkText");
        return chunkText.Length <= 240 ? chunkText : $"{chunkText[..240]}...";
    }

    private static string GetString(BsonDocument document, string fieldName)
    {
        return GetOptionalString(document, fieldName) ?? string.Empty;
    }

    private static string? GetOptionalString(BsonDocument document, string fieldName)
    {
        return document.TryGetValue(fieldName, out var value) && value.IsString
            ? value.AsString
            : null;
    }
}
