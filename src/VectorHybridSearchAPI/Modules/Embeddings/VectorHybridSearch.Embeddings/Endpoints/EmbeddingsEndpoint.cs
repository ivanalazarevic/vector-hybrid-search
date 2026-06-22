using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Embeddings;

namespace VectorHybridSearch.Embeddings.Endpoints;

public sealed class EmbeddingsEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embeddings")
            .WithTags("Embeddings");

        group.MapGet("/model", (IEmbeddingService embeddingService) => {
            var model = embeddingService.GetModelInfo();

            return Results.Ok(new EmbeddingModelInfoResponse(
                Provider: model.Provider,
                Model: model.Model,
                Dimensions: model.Dimensions,
                IsPlaceholder: model.IsPlaceholder));
        });

        group.MapPost("/generate", async (
            GenerateEmbeddingRequest request,
            IEmbeddingService embeddingService,
            CancellationToken cancellationToken) => {
            try {
                var result = await embeddingService.GenerateAsync(
                    request.Input,
                    cancellationToken);

                return Results.Ok(new GenerateEmbeddingResponse(
                    Model: result.Model,
                    Dimensions: result.Dimensions,
                    Vector: result.Vector,
                    GeneratedAt: result.GeneratedAt));
            }
            catch (ArgumentException exception) {
                return Results.BadRequest(new {
                    Error = exception.Message
                });
            }
        });
    }
}
