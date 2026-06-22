namespace VectorHybridSearch.Embeddings.Services;

public interface IEmbeddingService
{
    EmbeddingModelInfo GetModelInfo();

    Task<EmbeddingResult> GenerateAsync(
        string input,
        CancellationToken cancellationToken);
}
