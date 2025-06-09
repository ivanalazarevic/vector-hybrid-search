using VectorHybridSearch.Shared.Api.Endpoints;

namespace VectorHybridSearch.DataIngestion.DataIngestion;

public class DataIngestionEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ingest",()=> Results.Ok("Hello World!"));
    }
}