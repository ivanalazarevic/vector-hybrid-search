namespace VectorHybridSearch.Elasticsearch.Indexing;

public interface IElasticsearchIndexingService
{
    Task<ElasticsearchIndexingResult> RebuildIndexAsync(CancellationToken cancellationToken);
}
