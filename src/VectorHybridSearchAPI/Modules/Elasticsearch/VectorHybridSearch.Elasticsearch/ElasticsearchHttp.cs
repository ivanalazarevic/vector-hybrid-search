using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VectorHybridSearch.Elasticsearch;

public static class ElasticsearchHttp
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static StringContent CreateJsonContent(object value)
    {
        return new StringContent(
            JsonSerializer.Serialize(value, JsonOptions),
            Encoding.UTF8,
            "application/json");
    }

    public static async Task<JsonDocument> PostSearchAsync(
        HttpClient httpClient,
        string indexName,
        object requestBody,
        string operation,
        CancellationToken cancellationToken)
    {
        using var content = CreateJsonContent(requestBody);

        using var response = await httpClient.PostAsync(
            $"{EscapeIndexName(indexName)}/_search",
            content,
            cancellationToken);

        await EnsureSuccessAsync(response, operation, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(responseBody);
    }

    public static async Task EnsureSuccessAsync(
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

    public static string EscapeIndexName(string indexName) => Uri.EscapeDataString(indexName);
}
