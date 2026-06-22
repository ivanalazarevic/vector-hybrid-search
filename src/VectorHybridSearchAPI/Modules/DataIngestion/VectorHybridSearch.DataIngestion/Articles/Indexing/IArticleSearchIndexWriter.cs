using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Indexing;

public interface IArticleSearchIndexWriter
{
    string Source { get; }

    Task<ArticleSearchIndexingResult> IndexArticleAsync(
        Article article,
        IReadOnlyCollection<ArticleChunk> chunks,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddings,
        CancellationToken cancellationToken);
}
