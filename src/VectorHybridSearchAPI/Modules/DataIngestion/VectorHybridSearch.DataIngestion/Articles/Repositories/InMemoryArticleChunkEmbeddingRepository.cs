using System.Collections.Concurrent;
using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Repositories;

public sealed class InMemoryArticleChunkEmbeddingRepository : IArticleChunkEmbeddingRepository
{
    private readonly ConcurrentDictionary<string, ArticleChunkEmbedding> embeddings = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertForArticleAsync(
        string articleId,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddingsToStore,
        CancellationToken cancellationToken)
    {
        var existingEmbeddings = embeddings.Values
            .Where(embedding => string.Equals(embedding.ArticleId, articleId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var embedding in existingEmbeddings) {
            cancellationToken.ThrowIfCancellationRequested();
            embeddings.TryRemove(embedding.Id, out _);
        }

        foreach (var embedding in embeddingsToStore) {
            cancellationToken.ThrowIfCancellationRequested();
            embeddings[embedding.Id] = embedding;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ArticleChunkEmbedding>> GetByArticleIdAsync(
        string articleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<ArticleChunkEmbedding> articleEmbeddings = embeddings.Values
            .Where(embedding => string.Equals(embedding.ArticleId, articleId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(embedding => embedding.ChunkIndex)
            .ToArray();

        return Task.FromResult(articleEmbeddings);
    }

    public Task<ArticleChunkEmbedding?> GetByChunkIdAsync(
        string chunkId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embedding = embeddings.Values.FirstOrDefault(
            candidate => string.Equals(candidate.ChunkId, chunkId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(embedding);
    }
}
