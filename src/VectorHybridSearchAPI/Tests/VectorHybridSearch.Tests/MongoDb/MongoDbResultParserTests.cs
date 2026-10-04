using MongoDB.Bson;
using VectorHybridSearch.MongoDb.Search;

namespace VectorHybridSearch.Tests.MongoDb;

public sealed class MongoDbResultParserTests
{
    [Fact]
    public void ParseResults_MapsDocumentsInOrderWithOneBasedRanks()
    {
        var documents = new[] {
            new BsonDocument {
                { "articleId", "a-1" },
                { "title", "First" },
                { "chunkText", "first text" },
                { "source", "BBC" },
                { "category", "tech" },
                { "score", 7.5 }
            },
            new BsonDocument {
                { "articleId", "a-2" },
                { "title", "Second" },
                { "chunkText", "second text" }
            }
        };

        var results = MongoDbResultParser.ParseResults(documents).ToArray();

        Assert.Equal(2, results.Length);

        Assert.Equal("a-1", results[0].ArticleId);
        Assert.Equal("First", results[0].Title);
        Assert.Equal("first text", results[0].Snippet);
        Assert.Equal(7.5, results[0].Score);
        Assert.Equal(1, results[0].Rank);
        Assert.Equal("BBC", results[0].Source);
        Assert.Equal("tech", results[0].Category);

        Assert.Equal("a-2", results[1].ArticleId);
        Assert.Equal(0d, results[1].Score);
        Assert.Equal(2, results[1].Rank);
        Assert.Null(results[1].Source);
        Assert.Null(results[1].Category);
    }

    [Fact]
    public void GetSnippet_WithHighlights_WrapsHitsInEmAndHtmlEncodesTheText()
    {
        var document = new BsonDocument {
            { "chunkText", "ignored when a highlight is present" },
            {
                "highlights", new BsonArray {
                    new BsonDocument {
                        { "path", "chunkText" },
                        {
                            "texts", new BsonArray {
                                new BsonDocument { { "value", "profits at M&S <rose> on " }, { "type", "text" } },
                                new BsonDocument { { "value", "interest" }, { "type", "hit" } }
                            }
                        }
                    }
                }
            }
        };

        var snippet = MongoDbResultParser.GetSnippet(document);

        Assert.Equal("profits at M&amp;S &lt;rose&gt; on <em>interest</em>", snippet);
    }

    [Fact]
    public void GetSnippet_IgnoresHighlightsOnOtherPaths()
    {
        var document = new BsonDocument {
            { "chunkText", "chunk text" },
            {
                "highlights", new BsonArray {
                    new BsonDocument {
                        { "path", "title" },
                        { "texts", new BsonArray { new BsonDocument { { "value", "title hit" }, { "type", "hit" } } } }
                    }
                }
            }
        };

        Assert.Equal("chunk text", MongoDbResultParser.GetSnippet(document));
    }

    [Fact]
    public void GetSnippet_WithoutHighlights_TruncatesChunkTextTo240Characters()
    {
        var document = new BsonDocument("chunkText", new string('x', 300));

        Assert.Equal(new string('x', 240) + "...", MongoDbResultParser.GetSnippet(document));
    }
}
