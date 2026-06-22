using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public interface IArticleChunkRepository
{
    Task UpsertForArticleAsync(
        string articleId,
        IReadOnlyCollection<ArticleChunk> chunks,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArticleChunk>> GetByArticleIdAsync(
        string articleId,
        CancellationToken cancellationToken);
}
