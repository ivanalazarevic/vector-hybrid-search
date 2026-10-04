using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.MongoDb.Indexing;
using VectorHybridSearch.MongoDb.Search;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Module;

namespace VectorHybridSearch.MongoDb;

public sealed class MongoDbModule : IModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(sp => {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection("MongoDb");

            return new MongoDbOptions(
                ConnectionString: section["ConnectionString"] ?? "mongodb://localhost:27017",
                DatabaseName: section["DatabaseName"] ?? "vector_hybrid_search",
                CollectionName: section["CollectionName"] ?? "article_chunks",
                SearchIndexName: section["SearchIndexName"] ?? "article_chunks_text",
                VectorSearchIndexName: section["VectorSearchIndexName"] ?? "article_chunks_vector",
                VectorDimensions: section.GetValue("VectorDimensions", 384));
        });

        services.AddSingleton<IMongoClient>(sp => {
            var options = sp.GetRequiredService<MongoDbOptions>();
            return new MongoClient(options.ConnectionString);
        });

        services.AddScoped<MongoDbIndexingService>();
        services.AddScoped<IMongoDbIndexingService>(sp => sp.GetRequiredService<MongoDbIndexingService>());
        services.AddScoped<IArticleSearchIndexWriter>(sp => sp.GetRequiredService<MongoDbIndexingService>());
        services.AddScoped<ISearchProvider, MongoDbBm25SearchProvider>();
        services.AddScoped<ISearchProvider, MongoDbVectorSearchProvider>();
    }
}
