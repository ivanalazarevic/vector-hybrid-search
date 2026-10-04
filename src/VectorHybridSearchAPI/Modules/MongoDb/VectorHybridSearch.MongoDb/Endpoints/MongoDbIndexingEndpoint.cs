using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using VectorHybridSearch.MongoDb.Indexing;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Indexing;

namespace VectorHybridSearch.MongoDb.Endpoints;

public sealed class MongoDbIndexingEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/indexing/mongodb")
            .WithTags("MongoDB indexing");

        group.MapPost("/rebuild", async (
            IMongoDbIndexingService indexingService,
            CancellationToken cancellationToken) => {
            try {
                var result = await indexingService.RebuildCollectionAsync(cancellationToken);

                var response = new MongoDbRebuildCollectionResponse(
                    DatabaseName: result.DatabaseName,
                    CollectionName: result.CollectionName,
                    Articles: result.Articles,
                    Chunks: result.Chunks,
                    IndexedDocuments: result.IndexedDocuments,
                    SkippedChunksWithoutEmbedding: result.SkippedChunksWithoutEmbedding,
                    Status: "MongoDB collection rebuilt from in-memory article chunks and embeddings.");

                return Results.Ok(response);
            }
            catch (TimeoutException exception) {
                return Results.Problem(
                    title: "MongoDB is not reachable.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (MongoException exception) {
                return Results.Problem(
                    title: "MongoDB indexing failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException exception) {
                return Results.Problem(
                    title: "MongoDB indexing failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
}
