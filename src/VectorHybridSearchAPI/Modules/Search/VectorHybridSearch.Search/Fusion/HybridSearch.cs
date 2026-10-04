using System.Globalization;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Fusion;

// The engine-independent part of hybrid search: option defaults, validation, and the application-level RRF strategy.
// Both engines' hybrid providers call RunRrfAsync, so the Rrf strategy is the same code for each engine.
public static class HybridSearch
{
    public static HybridOptions GetOptions(SearchRequest request) => request.Hybrid ?? new HybridOptions();

    public static string? Validate(HybridOptions options)
    {
        if (!Enum.IsDefined(options.Strategy)) {
            return "Hybrid.Strategy must be Rrf or Native.";
        }

        if (!IsValidWeight(options.Bm25Weight) || !IsValidWeight(options.VectorWeight)) {
            return "Hybrid.Bm25Weight and Hybrid.VectorWeight must be zero or greater.";
        }

        if (options.Bm25Weight == 0d && options.VectorWeight == 0d) {
            return "Hybrid.Bm25Weight and Hybrid.VectorWeight cannot both be zero.";
        }

        if (options.RrfK is < 1 or > 1000) {
            return "Hybrid.RrfK must be between 1 and 1000.";
        }

        return null;
    }

    // How many articles each list contributes to the fusion, so that articles ranked below TopK in one list can still be fused.
    public static int GetCandidateDepth(int topK) => Math.Max(topK * 5, 50);

    public static async Task<SearchProviderResult> RunRrfAsync(
        ISearchProvider bm25Provider,
        ISearchProvider vectorProvider,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        var options = GetOptions(request);
        var candidateDepth = GetCandidateDepth(request.TopK);

        var bm25Task = bm25Provider.SearchAsync(
            request with { Mode = SearchMode.Bm25, TopK = candidateDepth },
            cancellationToken);
        var vectorTask = vectorProvider.SearchAsync(
            request with { Mode = SearchMode.Vector, TopK = candidateDepth },
            cancellationToken);

        await Task.WhenAll(bm25Task, vectorTask);

        var bm25 = await bm25Task;
        var vector = await vectorTask;

        var metadata = new Dictionary<string, string> {
            ["mode"] = nameof(SearchMode.Hybrid),
            ["engine"] = request.Engine.ToString(),
            ["hybridStrategy"] = nameof(HybridStrategy.Rrf),
            ["bm25Weight"] = options.Bm25Weight.ToString(CultureInfo.InvariantCulture),
            ["vectorWeight"] = options.VectorWeight.ToString(CultureInfo.InvariantCulture),
            ["rrfK"] = options.RrfK.ToString(CultureInfo.InvariantCulture),
            ["candidateDepth"] = candidateDepth.ToString(CultureInfo.InvariantCulture),
            ["bm25Candidates"] = bm25.Results.Count.ToString(CultureInfo.InvariantCulture),
            ["vectorCandidates"] = vector.Results.Count.ToString(CultureInfo.InvariantCulture)
        };

        AddPrefixed(metadata, "bm25", bm25.Diagnostics.Metadata);
        AddPrefixed(metadata, "vector", vector.Diagnostics.Metadata);

        return new SearchProviderResult(
            Results: RankFusion.Fuse(bm25.Results, vector.Results, options, request.TopK),
            Diagnostics: new SearchDiagnostics(
                Strategy: $"Application-level weighted reciprocal rank fusion of {request.Engine} BM25 and vector results",
                RequestedTopK: request.TopK,
                Metadata: metadata));
    }

    private static bool IsValidWeight(double weight) => double.IsFinite(weight) && weight >= 0d;

    private static void AddPrefixed(
        Dictionary<string, string> metadata,
        string prefix,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var (key, value) in source) {
            if (key is "mode" or "engine") {
                continue;
            }

            metadata[$"{prefix}.{key}"] = value;
        }
    }
}
