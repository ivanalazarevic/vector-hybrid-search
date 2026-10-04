namespace VectorHybridSearch.MongoDb.Indexing;

public sealed record MongoDbIndexingResult(
    string DatabaseName,
    string CollectionName,
    int Articles,
    int Chunks,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding);
