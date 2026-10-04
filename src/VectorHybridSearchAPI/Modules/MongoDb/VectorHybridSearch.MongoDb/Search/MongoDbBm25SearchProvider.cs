using System.Net;
using System.Text;
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

        var pipeline = new[] {
            BuildSearchStage(request),
            BuildProjectionStage(),
            BuildScoreSortStage(),
            BuildArticleGroupingStage(),
            BuildReplaceRootStage(),
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

    private BsonDocument BuildSearchStage(SearchRequest request)
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

        var filters = BuildFilters(request.Filters);
        if (filters.Count > 0) {
            compound.Add("filter", filters);
        }

        return new BsonDocument(
            "$search",
            new BsonDocument {
                { "index", options.SearchIndexName },
                { "compound", compound },
                { "highlight", new BsonDocument("path", "chunkText") }
            });
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
                { "score", new BsonDocument("$meta", "searchScore") },
                { "highlights", new BsonDocument("$meta", "searchHighlights") }
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

    private static BsonDocument BuildReplaceRootStage()
    {
        return new BsonDocument(
            "$replaceWith",
            "$bestChunk");
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

    private static BsonArray BuildFilters(SearchFilters? filters)
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
        if (document.TryGetValue("highlights", out var highlightsValue)
            && highlightsValue.IsBsonArray) {
            foreach (var highlightValue in highlightsValue.AsBsonArray) {
                if (!highlightValue.IsBsonDocument) {
                    continue;
                }

                var highlight = highlightValue.AsBsonDocument;
                if (GetOptionalString(highlight, "path") != "chunkText"
                    || !highlight.TryGetValue("texts", out var textsValue)
                    || !textsValue.IsBsonArray) {
                    continue;
                }

                var snippet = BuildHighlightedText(textsValue.AsBsonArray);
                if (!string.IsNullOrWhiteSpace(snippet)) {
                    return snippet;
                }
            }
        }

        var chunkText = GetString(document, "chunkText");
        return chunkText.Length <= 240 ? chunkText : $"{chunkText[..240]}...";
    }

    private static string BuildHighlightedText(BsonArray texts)
    {
        var result = new StringBuilder();

        foreach (var textValue in texts) {
            if (!textValue.IsBsonDocument) {
                continue;
            }

            var text = textValue.AsBsonDocument;
            var value = WebUtility.HtmlEncode(GetString(text, "value"));

            if (GetOptionalString(text, "type") == "hit") {
                result.Append("<em>").Append(value).Append("</em>");
            }
            else {
                result.Append(value);
            }
        }

        return result.ToString();
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
