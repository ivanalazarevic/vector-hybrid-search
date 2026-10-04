using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Search.Search;

public sealed class SearchEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search");

        group.MapPost("/", async (
            SearchRequest request,
            IEnumerable<ISearchProvider> searchProviders,
            CancellationToken cancellationToken) => {
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

            if (request.Engine == SearchEngine.Both) {
                return Results.BadRequest(new {
                    Error = "SearchEngine.Both is not implemented yet. Choose Elasticsearch or MongoDbAtlas."
                });
            }

            var providersForEngine = searchProviders
                .Where(provider => provider.Engine == request.Engine)
                .ToArray();

            if (providersForEngine.Length == 0) {
                return Results.BadRequest(new {
                    Error = $"No search provider is registered for {request.Engine}."
                });
            }

            var searchProvider = providersForEngine.FirstOrDefault(provider => provider.Supports(request.Mode));
            if (searchProvider is null) {
                return Results.BadRequest(new {
                    Error = $"{request.Engine} search currently does not support {request.Mode} mode."
                });
            }

            try {
                var stopwatch = Stopwatch.StartNew();
                var providerResult = await searchProvider.SearchAsync(request, cancellationToken);
                stopwatch.Stop();

                var response = new SearchResponse(
                    QueryId: Guid.NewGuid().ToString("N"),
                    Query: request.Query,
                    Engine: request.Engine,
                    Mode: request.Mode,
                    ElapsedMs: stopwatch.ElapsedMilliseconds,
                    Results: providerResult.Results,
                    Diagnostics: request.IncludeDiagnostics ? providerResult.Diagnostics : null);

                return Results.Ok(response);
            }
            catch (HttpRequestException exception) {
                return Results.Problem(
                    title: $"{request.Engine} is not reachable.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException exception) {
                return Results.Problem(
                    title: $"{request.Engine} search failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
}
