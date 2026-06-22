using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public interface IArticleChunkEmbeddingRepository
{
    Task UpsertForArticleAsync(
        string articleId,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddings,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArticleChunkEmbedding>> GetByArticleIdAsync(
        string articleId,
        CancellationToken cancellationToken);

    Task<ArticleChunkEmbedding?> GetByChunkIdAsync(
        string chunkId,
        CancellationToken cancellationToken);
}
