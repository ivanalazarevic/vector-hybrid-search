namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record IngestArticlesRequest(IReadOnlyCollection<ArticleDto> Articles);
