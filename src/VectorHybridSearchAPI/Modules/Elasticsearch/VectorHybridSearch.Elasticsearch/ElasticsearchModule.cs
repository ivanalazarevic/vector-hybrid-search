using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.Elasticsearch.Indexing;
using VectorHybridSearch.Elasticsearch.Search;
using VectorHybridSearch.Search.Providers;
using VectorHybridSearch.Shared.Module;

namespace VectorHybridSearch.Elasticsearch;

public sealed class ElasticsearchModule : IModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(sp => {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection("Elasticsearch");

            return new ElasticsearchOptions(
                BaseUrl: section["BaseUrl"] ?? "http://localhost:9200",
                IndexName: section["IndexName"] ?? "vector-hybrid-articles",
                Username: section["Username"],
                Password: section["Password"],
                ApiKey: section["ApiKey"],
                RequestTimeoutSeconds: int.TryParse(section["RequestTimeoutSeconds"], out var timeoutSeconds)
                    ? timeoutSeconds
                    : 30);
        });

        services.AddHttpClient<ElasticsearchIndexingService>(ConfigureElasticsearchHttpClient);
        services.AddHttpClient<ElasticsearchBm25SearchProvider>(ConfigureElasticsearchHttpClient);
        services.AddHttpClient<ElasticsearchVectorSearchProvider>(ConfigureElasticsearchHttpClient);
        services.AddHttpClient<ElasticsearchHybridSearchProvider>(ConfigureElasticsearchHttpClient);

        services.AddScoped<IElasticsearchIndexingService>(sp => sp.GetRequiredService<ElasticsearchIndexingService>());
        services.AddScoped<IArticleSearchIndexWriter>(sp => sp.GetRequiredService<ElasticsearchIndexingService>());
        services.AddScoped<ISearchProvider>(sp => sp.GetRequiredService<ElasticsearchBm25SearchProvider>());
        services.AddScoped<ISearchProvider>(sp => sp.GetRequiredService<ElasticsearchVectorSearchProvider>());
        services.AddScoped<ISearchProvider>(sp => sp.GetRequiredService<ElasticsearchHybridSearchProvider>());
    }

    private static void ConfigureElasticsearchHttpClient(IServiceProvider sp, HttpClient client)
    {
        var options = sp.GetRequiredService<ElasticsearchOptions>();

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(options.ApiKey)) {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", options.ApiKey);
        }
        else if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password)) {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
