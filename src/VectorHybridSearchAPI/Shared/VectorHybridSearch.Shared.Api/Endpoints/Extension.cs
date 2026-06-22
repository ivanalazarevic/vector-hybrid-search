using Microsoft.AspNetCore.Routing;

namespace VectorHybridSearch.Shared.Api.Endpoints;

public static class Extension
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointType = typeof(IEndpoint);
        AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsInterface: false, IsAbstract: false } && endpointType.IsAssignableFrom(t))
            .Select(t => Activator.CreateInstance(t) as IEndpoint)
            .Where(e => e is not null)
            .ToList()
            .ForEach(e => e!.AddRoutes(app));

        return app;
    }
}
