namespace VectorHybridSearch.Shared.Contracts.Embeddings;

public sealed record EmbeddingModelInfoResponse(
    string Provider,
    string Model,
    int Dimensions,
    bool IsPlaceholder);
