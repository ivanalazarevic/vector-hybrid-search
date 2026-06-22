using System.Collections.Concurrent;
using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public sealed class InMemoryArticleChunkRepository : IArticleChunkRepository
{
    private readonly ConcurrentDictionary<string, ArticleChunk> chunks = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertForArticleAsync(
        string articleId,
        IReadOnlyCollection<ArticleChunk> chunksToStore,
        CancellationToken cancellationToken)
    {
        var existingChunks = chunks.Values
            .Where(chunk => string.Equals(chunk.ArticleId, articleId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var chunk in existingChunks) {
            cancellationToken.ThrowIfCancellationRequested();
            chunks.TryRemove(chunk.Id, out _);
        }

        foreach (var chunk in chunksToStore) {
            cancellationToken.ThrowIfCancellationRequested();
            chunks[chunk.Id] = chunk;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ArticleChunk>> GetByArticleIdAsync(
        string articleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<ArticleChunk> articleChunks = chunks.Values
            .Where(chunk => string.Equals(chunk.ArticleId, articleId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToArray();

        return Task.FromResult(articleChunks);
    }
}
