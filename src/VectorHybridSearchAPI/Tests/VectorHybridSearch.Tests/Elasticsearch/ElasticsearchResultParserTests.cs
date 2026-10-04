using System.Text.Json;
using VectorHybridSearch.Elasticsearch.Search;

namespace VectorHybridSearch.Tests.Elasticsearch;

public sealed class ElasticsearchResultParserTests
{
    [Fact]
    public void ParseResults_MapsHitsInOrderWithOneBasedRanks()
    {
        using var json = JsonDocument.Parse("""
            {
              "hits": {
                "hits": [
                  {
                    "_score": 12.5,
                    "_source": { "articleId": "a-1", "title": "First", "chunkText": "first text", "source": "BBC", "category": "tech" },
                    "highlight": { "chunkText": ["<em>first</em> text"] }
                  },
                  {
                    "_score": 3.25,
                    "_source": { "articleId": "a-2", "title": "Second", "chunkText": "second text", "source": null }
                  }
                ]
              }
            }
            """);

        var results = ElasticsearchResultParser.ParseResults(json.RootElement).ToArray();

        Assert.Equal(2, results.Length);

        Assert.Equal("a-1", results[0].ArticleId);
        Assert.Equal("First", results[0].Title);
        Assert.Equal("<em>first</em> text", results[0].Snippet);
        Assert.Equal(12.5, results[0].Score);
        Assert.Equal(1, results[0].Rank);
        Assert.Equal("BBC", results[0].Source);
        Assert.Equal("tech", results[0].Category);

        Assert.Equal("a-2", results[1].ArticleId);
        Assert.Equal("second text", results[1].Snippet);
        Assert.Equal(2, results[1].Rank);
        Assert.Null(results[1].Source);
        Assert.Null(results[1].Category);
    }

    [Fact]
    public void ParseResults_WithoutScore_UsesZero()
    {
        using var json = JsonDocument.Parse("""
            { "hits": { "hits": [ { "_source": { "articleId": "a-1", "title": "First", "chunkText": "text" } } ] } }
            """);

        var result = Assert.Single(ElasticsearchResultParser.ParseResults(json.RootElement));

        Assert.Equal(0d, result.Score);
    }

    [Fact]
    public void GetSnippet_WithoutHighlight_TruncatesChunkTextTo240Characters()
    {
        var chunkText = new string('x', 300);
        using var json = JsonDocument.Parse($$"""
            { "_source": { "chunkText": "{{chunkText}}" } }
            """);

        var snippet = ElasticsearchResultParser.GetSnippet(json.RootElement, json.RootElement.GetProperty("_source"));

        Assert.Equal(new string('x', 240) + "...", snippet);
    }
}
