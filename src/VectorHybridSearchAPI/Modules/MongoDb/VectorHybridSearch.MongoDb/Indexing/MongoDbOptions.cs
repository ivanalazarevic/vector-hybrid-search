namespace VectorHybridSearch.MongoDb.Indexing;

public sealed record MongoDbOptions(
    string ConnectionString,
    string DatabaseName,
    string CollectionName,
    string SearchIndexName,
    string VectorSearchIndexName,
    int VectorDimensions);
