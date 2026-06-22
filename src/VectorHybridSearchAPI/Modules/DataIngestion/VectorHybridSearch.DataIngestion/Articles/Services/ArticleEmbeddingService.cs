using VectorHybridSearch.DataIngestion.Articles.Domain;
using VectorHybridSearch.Embeddings.Services;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public sealed class ArticleEmbeddingService(IEmbeddingService embeddingService) : IArticleEmbeddingService
{
    public async Task<IReadOnlyCollection<ArticleChunkEmbedding>> GenerateForChunksAsync(
        IReadOnlyCollection<ArticleChunk> chunks,
        CancellationToken cancellationToken)
    {
        var embeddings = new List<ArticleChunkEmbedding>(chunks.Count);

        foreach (var chunk in chunks.OrderBy(chunk => chunk.ChunkIndex)) {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding = await embeddingService.GenerateAsync(chunk.Text, cancellationToken);

            embeddings.Add(new ArticleChunkEmbedding(
                Id: $"{chunk.Id}:embedding",
                ArticleId: chunk.ArticleId,
                ChunkId: chunk.Id,
                ChunkIndex: chunk.ChunkIndex,
                Model: embedding.Model,
                Dimensions: embedding.Dimensions,
                Vector: embedding.Vector,
                GeneratedAt: embedding.GeneratedAt));
        }

        return embeddings;
    }
}
