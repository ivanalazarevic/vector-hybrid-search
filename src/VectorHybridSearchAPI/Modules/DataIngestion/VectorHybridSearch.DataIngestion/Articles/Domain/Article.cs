namespace VectorHybridSearch.DataIngestion.Articles.Domain;

public sealed record Article(
    string Id,
    string Title,
    string Content,
    string? Summary,
    string? Author,
    string? Source,
    string? Url,
    string? Category,
    DateTimeOffset? PublishedAt,
    DateTimeOffset IngestedAt);
