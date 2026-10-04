using System.Text.Json.Serialization;

namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchResultDto(
    string ArticleId,
    string Title,
    string Snippet,
    double Score,
    int Rank,
    string? Source,
    string? Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Bm25Rank = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? VectorRank = null);
