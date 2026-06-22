using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public interface IArticleChunkingService
{
    IReadOnlyCollection<ArticleChunk> CreateChunks(Article article);
}
