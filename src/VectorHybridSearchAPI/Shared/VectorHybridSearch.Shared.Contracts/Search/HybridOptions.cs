namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record HybridOptions(
    HybridStrategy Strategy = HybridStrategy.Rrf,
    double Bm25Weight = 1,
    double VectorWeight = 1,
    int RrfK = 60);
