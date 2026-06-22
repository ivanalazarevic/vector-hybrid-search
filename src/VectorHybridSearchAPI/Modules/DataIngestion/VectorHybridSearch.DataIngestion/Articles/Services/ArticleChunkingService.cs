using VectorHybridSearch.DataIngestion.Articles.Domain;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public sealed class ArticleChunkingService : IArticleChunkingService
{
    private const int ChunkSizeWords = 250;
    private const int OverlapWords = 50;

    public IReadOnlyCollection<ArticleChunk> CreateChunks(Article article)
    {
        var words = article.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) {
            return [];
        }

        var chunks = new List<ArticleChunk>();
        var stepSize = ChunkSizeWords - OverlapWords;
        var chunkIndex = 0;

        for (var start = 0; start < words.Length; start += stepSize) {
            var chunkWords = words
                .Skip(start)
                .Take(ChunkSizeWords)
                .ToArray();

            chunks.Add(new ArticleChunk(
                Id: $"{article.Id}:chunk-{chunkIndex:D4}",
                ArticleId: article.Id,
                ChunkIndex: chunkIndex,
                Text: string.Join(' ', chunkWords),
                WordCount: chunkWords.Length));

            chunkIndex++;

            if (start + ChunkSizeWords >= words.Length) {
                break;
            }
        }

        return chunks;
    }
}
