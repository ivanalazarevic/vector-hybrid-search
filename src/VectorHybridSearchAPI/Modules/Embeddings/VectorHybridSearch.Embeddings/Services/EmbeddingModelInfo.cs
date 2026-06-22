namespace VectorHybridSearch.Embeddings.Services;

public sealed record EmbeddingModelInfo(
    string Provider,
    string Model,
    int Dimensions,
    bool IsPlaceholder);
