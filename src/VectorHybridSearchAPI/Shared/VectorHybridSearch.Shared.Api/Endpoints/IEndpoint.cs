using Microsoft.AspNetCore.Routing;

namespace VectorHybridSearch.Shared.Api.Endpoints;

public interface IEndpoint
{
    void AddRoutes(IEndpointRouteBuilder routeBuilder);
}
