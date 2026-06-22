namespace VectorHybridSearch.Shared.Contracts.Embeddings;

public sealed record GenerateEmbeddingResponse(
    string Model,
    int Dimensions,
    IReadOnlyCollection<float> Vector,
    DateTimeOffset GeneratedAt);
