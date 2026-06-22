using System.Collections.Concurrent;
using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public sealed class InMemoryArticleRepository : IArticleRepository
{
    private readonly ConcurrentDictionary<string, Article> articles = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertManyAsync(IReadOnlyCollection<Article> articlesToStore, CancellationToken cancellationToken)
    {
        foreach (var article in articlesToStore) {
            cancellationToken.ThrowIfCancellationRequested();
            articles[article.Id] = article;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Article>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Article> storedArticles = articles.Values
            .OrderByDescending(article => article.IngestedAt)
            .ThenBy(article => article.Title)
            .ToArray();

        return Task.FromResult(storedArticles);
    }
}
