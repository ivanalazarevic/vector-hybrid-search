namespace VectorHybridSearch.Shared.Contracts.Indexing;

public sealed record MongoDbRebuildCollectionResponse(
    string DatabaseName,
    string CollectionName,
    int Articles,
    int Chunks,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding,
    string Status);
