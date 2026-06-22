using Microsoft.Extensions.DependencyInjection;
using VectorHybridSearch.DataIngestion.Articles.Imports;
using VectorHybridSearch.DataIngestion.Articles.Repositories;
using VectorHybridSearch.DataIngestion.Articles.Services;
using VectorHybridSearch.Shared.Module;

namespace VectorHybridSearch.DataIngestion;

public class DataIngestionModule : IModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IArticleRepository, InMemoryArticleRepository>();
        services.AddSingleton<IArticleChunkRepository, InMemoryArticleChunkRepository>();
        services.AddSingleton<IArticleChunkEmbeddingRepository, InMemoryArticleChunkEmbeddingRepository>();
        services.AddSingleton<IArticleChunkingService, ArticleChunkingService>();
        services.AddScoped<IArticleEmbeddingService, ArticleEmbeddingService>();
        services.AddScoped<IArticleIngestionService, ArticleIngestionService>();
        services.AddScoped<IBbcNewsImportService, BbcNewsImportService>();
    }
}
