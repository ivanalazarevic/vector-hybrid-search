using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Indexing;

namespace VectorHybridSearch.Elasticsearch.Endpoints;

public sealed class ElasticsearchIndexingEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/indexing/elasticsearch")
            .WithTags("Elasticsearch indexing");

        group.MapPost("/rebuild", async (
            IElasticsearchIndexingService indexingService,
            CancellationToken cancellationToken) => {
            try {
                var result = await indexingService.RebuildIndexAsync(cancellationToken);

                var response = new ElasticsearchRebuildIndexResponse(
                    IndexName: result.IndexName,
                    Articles: result.Articles,
                    Chunks: result.Chunks,
                    IndexedDocuments: result.IndexedDocuments,
                    SkippedChunksWithoutEmbedding: result.SkippedChunksWithoutEmbedding,
                    Status: "Elasticsearch index rebuilt from in-memory article chunks and embeddings.");

                return Results.Ok(response);
            }
            catch (HttpRequestException exception) {
                return Results.Problem(
                    title: "Elasticsearch is not reachable.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException exception) {
                return Results.Problem(
                    title: "Elasticsearch indexing failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
}
