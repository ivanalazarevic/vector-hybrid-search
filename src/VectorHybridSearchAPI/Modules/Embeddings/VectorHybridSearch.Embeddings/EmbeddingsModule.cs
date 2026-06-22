using Microsoft.Extensions.DependencyInjection;
using VectorHybridSearch.Embeddings.Services;
using VectorHybridSearch.Shared.Module;

namespace VectorHybridSearch.Embeddings;

public sealed class EmbeddingsModule : IModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingService, PlaceholderEmbeddingService>();
    }
}
