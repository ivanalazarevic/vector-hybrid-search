using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public interface IArticleEmbeddingService
{
    Task<IReadOnlyCollection<ArticleChunkEmbedding>> GenerateForChunksAsync(
        IReadOnlyCollection<ArticleChunk> chunks,
        CancellationToken cancellationToken);
}
