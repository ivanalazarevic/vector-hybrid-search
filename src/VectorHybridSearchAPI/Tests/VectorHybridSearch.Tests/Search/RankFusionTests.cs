using VectorHybridSearch.Search.Fusion;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Tests.Search;

public sealed class RankFusionTests
{
    [Fact]
    public void Fuse_ScoresEachArticleByItsWeightedReciprocalRanks()
    {
        var bm25 = Hits("a", "b");
        var vector = Hits("b", "c");

        var fused = RankFusion.Fuse(bm25, vector, new HybridOptions(Bm25Weight: 2, VectorWeight: 0.5, RrfK: 10), topK: 10).ToArray();

        // b: 2/(10+2) + 0.5/(10+1), a: 2/(10+1), c: 0.5/(10+2)
        Assert.Equal(["b", "a", "c"], fused.Select(result => result.ArticleId));
        Assert.Equal((2d / 12) + (0.5 / 11), fused[0].Score, precision: 12);
        Assert.Equal(2d / 11, fused[1].Score, precision: 12);
        Assert.Equal(0.5 / 12, fused[2].Score, precision: 12);
        Assert.Equal([1, 2, 3], fused.Select(result => result.Rank));
    }

    [Fact]
    public void Fuse_ReportsTheRankInEachListAndLeavesAMissingListEmpty()
    {
        var fused = RankFusion.Fuse(Hits("a", "b"), Hits("b", "c"), new HybridOptions(), topK: 10)
            .ToDictionary(result => result.ArticleId);

        Assert.Equal((1, null), (fused["a"].Bm25Rank, fused["a"].VectorRank));
        Assert.Equal((2, 1), (fused["b"].Bm25Rank, fused["b"].VectorRank));
        Assert.Equal((null, 2), (fused["c"].Bm25Rank, fused["c"].VectorRank));
    }

    [Fact]
    public void Fuse_BreaksScoreTiesOnArticleId()
    {
        // With equal weights, "x" (BM25 rank 1) and "m" (vector rank 1) get the same score.
        var fused = RankFusion.Fuse(Hits("x"), Hits("m"), new HybridOptions(), topK: 10);

        Assert.Equal(["m", "x"], fused.Select(result => result.ArticleId));
    }

    [Fact]
    public void Fuse_TakesTheSnippetFromTheBm25HitWhenTheArticleIsInBothLists()
    {
        var bm25 = new[] { Hit("a", snippet: "a <em>highlighted</em> snippet") };
        var vector = new[] { Hit("a", snippet: "plain chunk start"), Hit("b", snippet: "vector only") };

        var fused = RankFusion.Fuse(bm25, vector, new HybridOptions(), topK: 10).ToDictionary(result => result.ArticleId);

        Assert.Equal("a <em>highlighted</em> snippet", fused["a"].Snippet);
        Assert.Equal("vector only", fused["b"].Snippet);
    }

    [Fact]
    public void Fuse_ReturnsAtMostTopK()
    {
        var fused = RankFusion.Fuse(Hits("a", "b", "c"), Hits("d", "e", "f"), new HybridOptions(), topK: 4);

        Assert.Equal(4, fused.Count);
        Assert.Equal([1, 2, 3, 4], fused.Select(result => result.Rank));
    }

    [Fact]
    public void Fuse_WithAZeroWeight_DropsArticlesFoundOnlyByThatList()
    {
        var fused = RankFusion.Fuse(Hits("a", "b"), Hits("c", "a"), new HybridOptions(VectorWeight: 0), topK: 10).ToArray();

        Assert.Equal(["a", "b"], fused.Select(result => result.ArticleId));
        Assert.Equal(2, fused[0].VectorRank);
    }

    [Fact]
    public void Fuse_CountsARepeatedArticleOnceAtItsBestPosition()
    {
        var fused = RankFusion.Fuse(Hits("a", "a", "b"), Hits(), new HybridOptions(), topK: 10).ToArray();

        Assert.Equal(["a", "b"], fused.Select(result => result.ArticleId));
        Assert.Equal([1, 2], fused.Select(result => result.Bm25Rank));
    }

    [Fact]
    public void Fuse_WithTwoEmptyLists_ReturnsEmpty()
    {
        Assert.Empty(RankFusion.Fuse(Hits(), Hits(), new HybridOptions(), topK: 10));
    }

    private static SearchResultDto[] Hits(params string[] articleIds)
    {
        return articleIds.Select(articleId => Hit(articleId, snippet: articleId)).ToArray();
    }

    private static SearchResultDto Hit(string articleId, string snippet)
    {
        return new SearchResultDto(
            ArticleId: articleId,
            Title: $"Title {articleId}",
            Snippet: snippet,
            Score: 1,
            Rank: 99,
            Source: "BBC",
            Category: "sport");
    }
}
