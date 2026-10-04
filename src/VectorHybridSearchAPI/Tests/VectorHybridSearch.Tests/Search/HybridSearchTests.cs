using System.Text.Json;
using VectorHybridSearch.Search.Fusion;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Tests.Search;

public sealed class HybridSearchTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GetOptions_WithoutHybridOptions_UsesEqualWeightRrf()
    {
        var options = HybridSearch.GetOptions(new SearchRequest(Query: "football"));

        Assert.Equal(new HybridOptions(HybridStrategy.Rrf, Bm25Weight: 1, VectorWeight: 1, RrfK: 60), options);
    }

    [Fact]
    public void Validate_AcceptsTheDefaultsAndASingleZeroWeight()
    {
        Assert.Null(HybridSearch.Validate(new HybridOptions()));
        Assert.Null(HybridSearch.Validate(new HybridOptions(HybridStrategy.Native, Bm25Weight: 0.3, VectorWeight: 0.7, RrfK: 20)));
        Assert.Null(HybridSearch.Validate(new HybridOptions(VectorWeight: 0)));
    }

    [Theory]
    [InlineData(0, 1, 1, 60)]
    [InlineData(3, 1, 1, 60)]
    [InlineData(1, -0.1, 1, 60)]
    [InlineData(1, 1, double.NaN, 60)]
    [InlineData(1, 0, 0, 60)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 1, 1, 1001)]
    public void Validate_RejectsOptionsThatCannotBeFused(int strategy, double bm25Weight, double vectorWeight, int rrfK)
    {
        var error = HybridSearch.Validate(new HybridOptions((HybridStrategy)strategy, bm25Weight, vectorWeight, rrfK));

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(10, 50)]
    [InlineData(11, 55)]
    [InlineData(100, 500)]
    public void GetCandidateDepth_IsFiveTimesTopKWithAFloorOfFifty(int topK, int expected)
    {
        Assert.Equal(expected, HybridSearch.GetCandidateDepth(topK));
    }

    [Fact]
    public async Task RunRrfAsync_QueriesBothProvidersAtCandidateDepthAndFusesTheirResults()
    {
        var bm25 = new StubProvider(SearchMode.Bm25, ["a", "b"]);
        var vector = new StubProvider(SearchMode.Vector, ["b", "c"]);
        var request = new SearchRequest(
            Query: "football",
            Engine: SearchEngine.MongoDbAtlas,
            Mode: SearchMode.Hybrid,
            TopK: 2,
            Filters: new SearchFilters(Category: "sport"),
            Hybrid: new HybridOptions(Bm25Weight: 0.3, VectorWeight: 0.7, RrfK: 20));

        var result = await HybridSearch.RunRrfAsync(bm25, vector, request, CancellationToken.None);

        Assert.Equal(request with { Mode = SearchMode.Bm25, TopK = 50 }, bm25.Received);
        Assert.Equal(request with { Mode = SearchMode.Vector, TopK = 50 }, vector.Received);

        // b: 0.3/22 + 0.7/21, c: 0.7/22, a: 0.3/21 — "a" falls outside TopK.
        Assert.Equal(["b", "c"], result.Results.Select(hit => hit.ArticleId));

        var metadata = result.Diagnostics.Metadata;
        Assert.Equal(2, result.Diagnostics.RequestedTopK);
        Assert.Equal("Rrf", metadata["hybridStrategy"]);
        Assert.Equal("MongoDbAtlas", metadata["engine"]);
        Assert.Equal("0.3", metadata["bm25Weight"]);
        Assert.Equal("0.7", metadata["vectorWeight"]);
        Assert.Equal("20", metadata["rrfK"]);
        Assert.Equal("50", metadata["candidateDepth"]);
        Assert.Equal("Bm25-index", metadata["bm25.index"]);
        Assert.Equal("Vector-index", metadata["vector.index"]);
    }

    [Fact]
    public async Task RunRrfAsync_PropagatesAProviderFailure()
    {
        var bm25 = new StubProvider(SearchMode.Bm25, ["a"]);
        var vector = new StubProvider(SearchMode.Vector, [], failure: new InvalidOperationException("index not ready"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => HybridSearch.RunRrfAsync(bm25, vector, new SearchRequest(Query: "football"), CancellationToken.None));

        Assert.Equal("index not ready", exception.Message);
    }

    [Fact]
    public void SearchRequest_ReadsHybridOptionsFromJsonAndDefaultsTheMissingOnes()
    {
        var request = JsonSerializer.Deserialize<SearchRequest>(
            """{ "query": "football", "engine": 2, "mode": 3, "hybrid": { "strategy": 2, "vectorWeight": 0.7 } }""",
            WebJson)!;

        Assert.Equal(new HybridOptions(HybridStrategy.Native, Bm25Weight: 1, VectorWeight: 0.7, RrfK: 60), request.Hybrid);
    }

    [Fact]
    public void SearchResultDto_WritesTheHybridRanksOnlyWhenTheyAreSet()
    {
        var result = new SearchResultDto(
            ArticleId: "a",
            Title: "Title",
            Snippet: "Snippet",
            Score: 1,
            Rank: 1,
            Source: null,
            Category: null);

        var plain = JsonSerializer.SerializeToElement(result, WebJson);
        var hybrid = JsonSerializer.SerializeToElement(result with { Bm25Rank = 3 }, WebJson);

        Assert.False(plain.TryGetProperty("bm25Rank", out _));
        Assert.False(plain.TryGetProperty("vectorRank", out _));
        Assert.Equal(3, hybrid.GetProperty("bm25Rank").GetInt32());
        Assert.False(hybrid.TryGetProperty("vectorRank", out _));
    }

    private sealed class StubProvider(SearchMode mode, string[] articleIds, Exception? failure = null) : ISearchProvider
    {
        public SearchRequest? Received { get; private set; }

        public SearchEngine Engine => SearchEngine.MongoDbAtlas;

        public bool Supports(SearchMode searchMode) => searchMode == mode;

        public Task<SearchProviderResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            Received = request;

            if (failure is not null) {
                return Task.FromException<SearchProviderResult>(failure);
            }

            var results = articleIds
                .Select((articleId, index) => new SearchResultDto(
                    ArticleId: articleId,
                    Title: articleId,
                    Snippet: articleId,
                    Score: 1,
                    Rank: index + 1,
                    Source: null,
                    Category: null))
                .ToArray();

            return Task.FromResult(new SearchProviderResult(
                Results: results,
                Diagnostics: new SearchDiagnostics(
                    Strategy: mode.ToString(),
                    RequestedTopK: request.TopK,
                    Metadata: new Dictionary<string, string> {
                        ["index"] = $"{mode}-index",
                        ["mode"] = mode.ToString()
                    })));
        }
    }
}
