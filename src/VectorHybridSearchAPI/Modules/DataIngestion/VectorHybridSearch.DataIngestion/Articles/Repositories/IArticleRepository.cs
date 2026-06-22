using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public interface IArticleRepository
{
    Task UpsertManyAsync(IReadOnlyCollection<Article> articles, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Article>> GetAllAsync(CancellationToken cancellationToken);
}
