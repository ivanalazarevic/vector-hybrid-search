namespace VectorHybridSearch.Elasticsearch.Indexing;

public sealed record ElasticsearchIndexingResult(
    string IndexName,
    int Articles,
    int Chunks,
    int IndexedDocuments,
    int SkippedChunksWithoutEmbedding);
