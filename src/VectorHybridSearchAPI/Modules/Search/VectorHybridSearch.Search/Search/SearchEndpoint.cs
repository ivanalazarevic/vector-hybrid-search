using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Search;

public sealed class SearchEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search");

        group.MapPost("/", (SearchRequest request) => {
            if (string.IsNullOrWhiteSpace(request.Query)) {
                return Results.BadRequest(new {
                    Error = "Search query is required."
                });
            }

            if (request.TopK is < 1 or > 100) {
                return Results.BadRequest(new {
                    Error = "TopK must be between 1 and 100."
                });
            }

            var stopwatch = Stopwatch.StartNew();
            stopwatch.Stop();

            var response = new SearchResponse(
                QueryId: Guid.NewGuid().ToString("N"),
                Query: request.Query,
                Engine: request.Engine,
                Mode: request.Mode,
                ElapsedMs: stopwatch.ElapsedMilliseconds,
                Results: [],
                Diagnostics: request.IncludeDiagnostics
                    ? new SearchDiagnostics(
                        Strategy: "Not implemented yet",
                        RequestedTopK: request.TopK,
                        Metadata: new Dictionary<string, string> {
                            ["next"] = "Wire this endpoint to Elasticsearch and MongoDB adapters."
                        })
                    : null);

            return Results.Ok(response);
        });
    }
}
