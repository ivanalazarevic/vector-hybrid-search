namespace VectorHybridSearch.Shared.Contracts.Articles;

public sealed record ArticleDto(
    string Id,
    string Title,
    string Content,
    string? Summary,
    string? Author,
    string? Source,
    string? Url,
    string? Category,
    DateTimeOffset? PublishedAt);
