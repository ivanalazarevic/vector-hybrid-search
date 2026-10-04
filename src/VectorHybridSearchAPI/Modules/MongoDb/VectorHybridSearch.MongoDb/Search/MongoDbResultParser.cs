using System.Net;
using System.Text;
using MongoDB.Bson;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.MongoDb.Search;

public static class MongoDbResultParser
{
    public static IReadOnlyCollection<SearchResultDto> ParseResults(
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

    public static string GetSnippet(BsonDocument document)
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
