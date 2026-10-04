using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Fusion;

public static class RankFusion
{
    // Weighted reciprocal rank fusion: score = bm25Weight / (rrfK + bm25Rank) + vectorWeight / (rrfK + vectorRank).
    // A rank is the article's 1-based position in its list; an article missing from a list gets no contribution from it.
    public static IReadOnlyCollection<SearchResultDto> Fuse(
        IReadOnlyCollection<SearchResultDto> bm25Results,
        IReadOnlyCollection<SearchResultDto> vectorResults,
        HybridOptions options,
        int topK)
    {
        var bm25Hits = RankByArticle(bm25Results);
        var vectorHits = RankByArticle(vectorResults);

        var fused = new List<SearchResultDto>(bm25Hits.Count + vectorHits.Count);

        foreach (var articleId in bm25Hits.Keys.Union(vectorHits.Keys, StringComparer.Ordinal)) {
            var hasBm25 = bm25Hits.TryGetValue(articleId, out var bm25);
            var hasVector = vectorHits.TryGetValue(articleId, out var vector);

            var score = (hasBm25 ? options.Bm25Weight / (options.RrfK + bm25.Rank) : 0d)
                + (hasVector ? options.VectorWeight / (options.RrfK + vector.Rank) : 0d);

            // A zero weight switches its list off, so an article found only by that list is not a result.
            if (score <= 0d) {
                continue;
            }

            // The BM25 hit carries the highlighted snippet, so it is preferred when the article is in both lists.
            var hit = hasBm25 ? bm25.Result : vector.Result;

            fused.Add(hit with {
                Score = score,
                Bm25Rank = hasBm25 ? bm25.Rank : null,
                VectorRank = hasVector ? vector.Rank : null
            });
        }

        return fused
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.ArticleId, StringComparer.Ordinal)
            .Take(topK)
            .Select((result, index) => result with { Rank = index + 1 })
            .ToArray();
    }

    private static Dictionary<string, (int Rank, SearchResultDto Result)> RankByArticle(
        IReadOnlyCollection<SearchResultDto> results)
    {
        var hits = new Dictionary<string, (int Rank, SearchResultDto Result)>(StringComparer.Ordinal);

        foreach (var result in results) {
            // Providers return one hit per article; if a list repeats one, its best position counts.
            hits.TryAdd(result.ArticleId, (hits.Count + 1, result));
        }

        return hits;
    }
}
