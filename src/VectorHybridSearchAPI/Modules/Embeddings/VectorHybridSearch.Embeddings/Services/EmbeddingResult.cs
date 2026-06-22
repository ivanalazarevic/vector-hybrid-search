namespace VectorHybridSearch.Embeddings.Services;

public sealed record EmbeddingResult(
    string Model,
    int Dimensions,
    IReadOnlyCollection<float> Vector,
    DateTimeOffset GeneratedAt);
