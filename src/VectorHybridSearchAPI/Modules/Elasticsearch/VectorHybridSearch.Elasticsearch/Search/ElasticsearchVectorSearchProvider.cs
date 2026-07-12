using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public sealed class ElasticsearchVectorSearchProvider(
    HttpClient httpClient,
    ElasticsearchOptions options,
    IEmbeddingService embeddingService) : ISearchProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SearchEngine Engine => SearchEngine.Elasticsearch;

    public bool Supports(SearchMode mode) => mode == SearchMode.Vector;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);
        var filters = BuildFilters(request.Filters);

        object knn = filters.Count == 0
            ? new {
                field = "embedding",
                query_vector = queryEmbedding.Vector,
                k = request.TopK,
                num_candidates = Math.Max(request.TopK * 10, 100)
            }
            : new {
                field = "embedding",
                query_vector = queryEmbedding.Vector,
                k = request.TopK,
                num_candidates = Math.Max(request.TopK * 10, 100),
                filter = filters
            };

        var requestBody = new {
            knn,
            size = request.TopK,
            _source = new[] {
                "articleId",
                "title",
                "chunkText",
                "source",
                "category"
            }
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.PostAsync(
            $"{EscapeIndexName(options.IndexName)}/_search",
            content,
            cancellationToken);

        await EnsureSuccessAsync(response, "run Elasticsearch vector search", cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(responseBody);

        var results = ParseResults(json.RootElement);

        return new SearchProviderResult(
            Results: results,
            Diagnostics: new SearchDiagnostics(
                Strategy: "Elasticsearch kNN vector search over embedding field",
                RequestedTopK: request.TopK,
                Metadata: new Dictionary<string, string> {
                    ["index"] = options.IndexName,
                    ["mode"] = nameof(SearchMode.Vector),
                    ["engine"] = nameof(SearchEngine.Elasticsearch),
                    ["embeddingModel"] = queryEmbedding.Model,
                    ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                    ["numCandidates"] = Math.Max(request.TopK * 10, 100).ToString(CultureInfo.InvariantCulture)
                }));
    }

    private static IReadOnlyCollection<object> BuildFilters(SearchFilters? filters)
    {
        if (filters is null) {
            return [];
        }

        var elasticFilters = new List<object>();

        if (!string.IsNullOrWhiteSpace(filters.Category)) {
            elasticFilters.Add(new {
                term = new Dictionary<string, object> {
                    ["category"] = filters.Category
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(filters.Source)) {
            elasticFilters.Add(new {
                term = new Dictionary<string, object> {
                    ["source"] = filters.Source
                }
            });
        }

        if (filters.PublishedFrom is not null || filters.PublishedTo is not null) {
            var range = new Dictionary<string, string>();

            if (filters.PublishedFrom is not null) {
                range["gte"] = filters.PublishedFrom.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            if (filters.PublishedTo is not null) {
                range["lte"] = filters.PublishedTo.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            elasticFilters.Add(new {
                range = new Dictionary<string, object> {
                    ["publishedAt"] = range
                }
            });
        }

        return elasticFilters;
    }

    private static IReadOnlyCollection<SearchResultDto> ParseResults(JsonElement root)
    {
        var hits = root
            .GetProperty("hits")
            .GetProperty("hits")
            .EnumerateArray();

        var results = new List<SearchResultDto>();
        var rank = 1;

        foreach (var hit in hits) {
            var source = hit.GetProperty("_source");
            var score = hit.TryGetProperty("_score", out var scoreElement)
                ? scoreElement.GetDouble()
                : 0d;

            results.Add(new SearchResultDto(
                ArticleId: GetRequiredString(source, "articleId"),
                Title: GetRequiredString(source, "title"),
                Snippet: GetSnippet(source),
                Score: score,
                Rank: rank,
                Source: GetOptionalString(source, "source"),
                Category: GetOptionalString(source, "category")));

            rank++;
        }

        return results;
    }

    private static string GetSnippet(JsonElement source)
    {
        var chunkText = GetRequiredString(source, "chunkText");
        return chunkText.Length <= 240 ? chunkText : $"{chunkText[..240]}...";
    }

    private static string GetRequiredString(JsonElement source, string propertyName)
    {
        var value = GetOptionalString(source, propertyName);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private static string? GetOptionalString(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null) {
            return null;
        }

        return value.GetString();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Failed to {operation}. Status {(int)response.StatusCode} {response.ReasonPhrase}. Response: {body}");
    }

    private static string EscapeIndexName(string indexName) => Uri.EscapeDataString(indexName);
}
