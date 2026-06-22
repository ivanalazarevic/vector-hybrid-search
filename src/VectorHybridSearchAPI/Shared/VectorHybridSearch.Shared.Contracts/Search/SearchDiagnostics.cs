namespace VectorHybridSearch.Shared.Contracts.Search;

public sealed record SearchDiagnostics(
    string Strategy,
    int RequestedTopK,
    IReadOnlyDictionary<string, string> Metadata);
