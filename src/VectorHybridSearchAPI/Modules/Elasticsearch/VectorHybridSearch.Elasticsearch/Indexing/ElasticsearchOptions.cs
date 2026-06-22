namespace VectorHybridSearch.Elasticsearch.Indexing;

public sealed record ElasticsearchOptions(
    string BaseUrl,
    string IndexName,
    string? Username,
    string? Password,
    string? ApiKey,
    int RequestTimeoutSeconds);
