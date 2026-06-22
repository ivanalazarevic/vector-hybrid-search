namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record ImportBbcNewsRequest(
    string FilePath,
    int MaxArticles = 1000);
