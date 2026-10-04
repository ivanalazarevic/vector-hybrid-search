using System.Text.Json;
using VectorHybridSearch.Elasticsearch;
using VectorHybridSearch.Elasticsearch.Search;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Tests.Elasticsearch;

public sealed class ElasticsearchQueryBuilderTests
{
    [Fact]
    public void BuildBoostedTextQuery_UsesTheBoostsSharedWithMongoDb()
    {
        var query = Serialize(ElasticsearchQueryBuilder.BuildBoostedTextQuery("prime minister"));

        var should = query.GetProperty("bool").GetProperty("should");
        var fields = should[0].GetProperty("multi_match").GetProperty("fields").EnumerateArray().Select(field => field.GetString());

        Assert.Equal(["chunkText^3", "title^4", "summary"], fields);
        Assert.Equal(8, should[1].GetProperty("match_phrase").GetProperty("title").GetProperty("boost").GetInt32());
        Assert.Equal(5, should[2].GetProperty("match_phrase").GetProperty("chunkText").GetProperty("boost").GetInt32());
        Assert.Equal(1, query.GetProperty("bool").GetProperty("minimum_should_match").GetInt32());
    }

    [Fact]
    public void BuildFilters_WithoutFilters_ReturnsEmpty()
    {
        Assert.Empty(ElasticsearchQueryBuilder.BuildFilters(null));
        Assert.Empty(ElasticsearchQueryBuilder.BuildFilters(new SearchFilters()));
    }

    [Fact]
    public void BuildFilters_MapsEveryFilterToItsField()
    {
        var filters = Serialize(ElasticsearchQueryBuilder.BuildFilters(new SearchFilters(
            Category: "tech",
            Source: "BBC",
            PublishedFrom: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            PublishedTo: new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero))));

        Assert.Equal(3, filters.GetArrayLength());
        Assert.Equal("tech", filters[0].GetProperty("term").GetProperty("category").GetString());
        Assert.Equal("BBC", filters[1].GetProperty("term").GetProperty("source").GetString());

        var range = filters[2].GetProperty("range").GetProperty("publishedAt");
        Assert.Equal("2024-01-01T00:00:00.0000000+00:00", range.GetProperty("gte").GetString());
        Assert.Equal("2024-12-31T00:00:00.0000000+00:00", range.GetProperty("lte").GetString());
    }

    [Fact]
    public void BuildFilteredTextQuery_WithoutFilters_IsTheBoostedTextQuery()
    {
        var request = new SearchRequest(Query: "football");

        var query = Serialize(ElasticsearchQueryBuilder.BuildFilteredTextQuery(request));

        Assert.True(query.GetProperty("bool").TryGetProperty("should", out _));
        Assert.False(query.GetProperty("bool").TryGetProperty("filter", out _));
    }

    [Fact]
    public void BuildFilteredTextQuery_WithFilters_WrapsTheTextQueryInMust()
    {
        var request = new SearchRequest(Query: "football", Filters: new SearchFilters(Category: "sport"));

        var query = Serialize(ElasticsearchQueryBuilder.BuildFilteredTextQuery(request));

        var boolQuery = query.GetProperty("bool");
        Assert.True(boolQuery.GetProperty("must")[0].GetProperty("bool").TryGetProperty("should", out _));
        Assert.Equal("sport", boolQuery.GetProperty("filter")[0].GetProperty("term").GetProperty("category").GetString());
    }

    [Fact]
    public void BuildChunkTextHighlight_HtmlEncodesSnippets()
    {
        var highlight = Serialize(ElasticsearchQueryBuilder.BuildChunkTextHighlight());

        Assert.Equal("html", highlight.GetProperty("encoder").GetString());
        Assert.Equal(220, highlight.GetProperty("fields").GetProperty("chunkText").GetProperty("fragment_size").GetInt32());
        Assert.Equal(1, highlight.GetProperty("fields").GetProperty("chunkText").GetProperty("number_of_fragments").GetInt32());
    }

    private static JsonElement Serialize(object value)
    {
        return JsonSerializer.SerializeToElement(value, ElasticsearchHttp.JsonOptions);
    }
}
