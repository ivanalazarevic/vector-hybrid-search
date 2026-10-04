using System.Text.Json;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public static class ElasticsearchResultParser
{
    public static IReadOnlyCollection<SearchResultDto> ParseResults(JsonElement root)
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

    public static string GetSnippet(JsonElement hit, JsonElement source)
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
}
