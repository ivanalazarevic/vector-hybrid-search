using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public sealed class ElasticsearchHybridSearchProvider(
    HttpClient httpClient,
    ElasticsearchOptions options,
    IEmbeddingService embeddingService) : ISearchProvider
{
    private const double Bm25Weight = 1.0d;
    private const double VectorWeight = 1.0d;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SearchEngine Engine => SearchEngine.Elasticsearch;

    public bool Supports(SearchMode mode) => mode == SearchMode.Hybrid;

    public async Task<SearchProviderResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateAsync(request.Query, cancellationToken);

        var requestBody = new {
            size = request.TopK,
            query = new {
                script_score = new {
                    query = BuildTextQuery(request),
                    script = new {
                        source = "(_score * params.bm25Weight) + ((cosineSimilarity(params.queryVector, 'embedding') + 1.0) * params.vectorWeight)",
                        @params = new {
                            queryVector = queryEmbedding.Vector,
                            bm25Weight = Bm25Weight,
                            vectorWeight = VectorWeight
                        }
                    }
                }
            },
            highlight = new {
                fields = new Dictionary<string, object> {
                    ["chunkText"] = new {
                        fragment_size = 220,
                        number_of_fragments = 1
                    }
                }
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

        await EnsureSuccessAsync(response, "run Elasticsearch hybrid search", cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(responseBody);

        var results = ParseResults(json.RootElement);

        return new SearchProviderResult(
            Results: results,
            Diagnostics: new SearchDiagnostics(
                Strategy: "Elasticsearch script_score hybrid: BM25 candidate selection plus cosine vector reranking",
                RequestedTopK: request.TopK,
                Metadata: new Dictionary<string, string> {
                    ["index"] = options.IndexName,
                    ["mode"] = nameof(SearchMode.Hybrid),
                    ["engine"] = nameof(SearchEngine.Elasticsearch),
                    ["embeddingModel"] = queryEmbedding.Model,
                    ["embeddingDimensions"] = queryEmbedding.Dimensions.ToString(CultureInfo.InvariantCulture),
                    ["bm25Weight"] = Bm25Weight.ToString(CultureInfo.InvariantCulture),
                    ["vectorWeight"] = VectorWeight.ToString(CultureInfo.InvariantCulture),
                    ["strategy"] = "script_score"
                }));
    }

    private static object BuildTextQuery(SearchRequest request)
    {
        var keywordQuery = new {
            multi_match = new {
                query = request.Query,
                fields = new[] {
                    "chunkText^3",
                    "title^2",
                    "summary"
                },
                type = "best_fields"
            }
        };

        var filters = BuildFilters(request.Filters);
        if (filters.Count == 0) {
            return keywordQuery;
        }

        return new {
            @bool = new {
                must = new object[] {
                    keywordQuery
                },
                filter = filters
            }
        };
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
                Snippet: GetSnippet(hit, source),
                Score: score,
                Rank: rank,
                Source: GetOptionalString(source, "source"),
                Category: GetOptionalString(source, "category")));

            rank++;
        }

        return results;
    }

    private static string GetSnippet(JsonElement hit, JsonElement source)
    {
        if (hit.TryGetProperty("highlight", out var highlight)
            && highlight.TryGetProperty("chunkText", out var chunkTextHighlights)
            && chunkTextHighlights.ValueKind == JsonValueKind.Array
            && chunkTextHighlights.GetArrayLength() > 0) {
            return chunkTextHighlights[0].GetString() ?? string.Empty;
        }

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
