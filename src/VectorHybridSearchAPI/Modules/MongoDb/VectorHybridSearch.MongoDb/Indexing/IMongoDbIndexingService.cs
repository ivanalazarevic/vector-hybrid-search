namespace VectorHybridSearch.MongoDb.Indexing;

public interface IMongoDbIndexingService
{
    Task<MongoDbIndexingResult> RebuildCollectionAsync(CancellationToken cancellationToken);
}
