using MongoDB.Bson;
using MongoDB.Driver;
using VectorHybridSearch.DataIngestion.Articles.Domain;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.DataIngestion.Articles.Repositories;
using VectorHybridSearch.Embeddings.Services;

namespace VectorHybridSearch.MongoDb.Indexing;

public sealed class MongoDbIndexingService(
    IMongoClient mongoClient,
    MongoDbOptions options,
    IEmbeddingService embeddingService,
    IArticleRepository articleRepository,
    IArticleChunkRepository articleChunkRepository,
    IArticleChunkEmbeddingRepository articleChunkEmbeddingRepository) : IMongoDbIndexingService, IArticleSearchIndexWriter
{
    private readonly SemaphoreSlim indexInitializationLock = new(1, 1);
    private bool indexesInitialized;

    public string Source => "MongoDB";

    public async Task<MongoDbIndexingResult> RebuildCollectionAsync(CancellationToken cancellationToken)
    {
        var documents = await BuildDocumentsAsync(cancellationToken);
        var collection = GetCollection();

        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.DeleteManyAsync(FilterDefinition<MongoDbSearchDocument>.Empty, cancellationToken);

        if (documents.Documents.Count > 0) {
            await UpsertDocumentsAsync(collection, documents.Documents, cancellationToken);
        }

        return new MongoDbIndexingResult(
            DatabaseName: options.DatabaseName,
            CollectionName: options.CollectionName,
            Articles: documents.ArticleCount,
            Chunks: documents.ChunkCount,
            IndexedDocuments: documents.Documents.Count,
            SkippedChunksWithoutEmbedding: documents.SkippedChunksWithoutEmbedding);
    }

    public async Task<ArticleSearchIndexingResult> IndexArticleAsync(
        Article article,
        IReadOnlyCollection<ArticleChunk> chunks,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddings,
        CancellationToken cancellationToken)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);

        var documents = BuildDocumentsForArticle(
            article,
            chunks,
            embeddings,
            out var skippedChunksWithoutEmbedding);

        if (documents.Count > 0) {
            await UpsertDocumentsAsync(collection, documents, cancellationToken);
        }

        return new ArticleSearchIndexingResult(
            Source: Source,
            IndexedDocuments: documents.Count,
            SkippedChunksWithoutEmbedding: skippedChunksWithoutEmbedding);
    }

    private async Task<BuildDocumentsResult> BuildDocumentsAsync(CancellationToken cancellationToken)
    {
        var articles = await articleRepository.GetAllAsync(cancellationToken);
        var documents = new List<MongoDbSearchDocument>();
        var chunkCount = 0;
        var skippedChunksWithoutEmbedding = 0;

        foreach (var article in articles) {
            cancellationToken.ThrowIfCancellationRequested();

            var chunks = await articleChunkRepository.GetByArticleIdAsync(article.Id, cancellationToken);
            var embeddings = await articleChunkEmbeddingRepository.GetByArticleIdAsync(article.Id, cancellationToken);

            chunkCount += chunks.Count;

            documents.AddRange(BuildDocumentsForArticle(
                article,
                chunks,
                embeddings,
                out var skippedForArticle));

            skippedChunksWithoutEmbedding += skippedForArticle;
        }

        return new BuildDocumentsResult(
            ArticleCount: articles.Count,
            ChunkCount: chunkCount,
            SkippedChunksWithoutEmbedding: skippedChunksWithoutEmbedding,
            Documents: documents);
    }

    private static IReadOnlyCollection<MongoDbSearchDocument> BuildDocumentsForArticle(
        Article article,
        IReadOnlyCollection<ArticleChunk> chunks,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddings,
        out int skippedChunksWithoutEmbedding)
    {
        var documents = new List<MongoDbSearchDocument>();
        var embeddingsByChunkId = embeddings.ToDictionary(
            embedding => embedding.ChunkId,
            StringComparer.OrdinalIgnoreCase);

        skippedChunksWithoutEmbedding = 0;

        foreach (var chunk in chunks) {
            if (!embeddingsByChunkId.TryGetValue(chunk.Id, out var embedding)) {
                skippedChunksWithoutEmbedding++;
                continue;
            }

            documents.Add(ToDocument(article, chunk, embedding));
        }

        return documents;
    }

    private async Task UpsertDocumentsAsync(
        IMongoCollection<MongoDbSearchDocument> collection,
        IReadOnlyCollection<MongoDbSearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var writes = documents
            .Select(document => new ReplaceOneModel<MongoDbSearchDocument>(
                Builders<MongoDbSearchDocument>.Filter.Eq(item => item.Id, document.Id),
                document) {
                IsUpsert = true
            })
            .ToArray();

        if (writes.Length == 0) {
            return;
        }

        await collection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
    }

    private async Task EnsureIndexesAsync(
        IMongoCollection<MongoDbSearchDocument> collection,
        CancellationToken cancellationToken)
    {
        if (indexesInitialized) {
            return;
        }

        await indexInitializationLock.WaitAsync(cancellationToken);

        try {
            if (indexesInitialized) {
                return;
            }

            var indexes = new[] {
                new CreateIndexModel<MongoDbSearchDocument>(
                    Builders<MongoDbSearchDocument>.IndexKeys.Ascending(document => document.ArticleId)),
                new CreateIndexModel<MongoDbSearchDocument>(
                    Builders<MongoDbSearchDocument>.IndexKeys.Ascending(document => document.ChunkId)),
                new CreateIndexModel<MongoDbSearchDocument>(
                    Builders<MongoDbSearchDocument>.IndexKeys.Ascending(document => document.Category)),
                new CreateIndexModel<MongoDbSearchDocument>(
                    Builders<MongoDbSearchDocument>.IndexKeys.Ascending(document => document.Source))
            };

            await collection.Indexes.CreateManyAsync(indexes, cancellationToken);
            await EnsureTextSearchIndexAsync(collection, cancellationToken);
            await EnsureVectorSearchIndexAsync(collection, cancellationToken);

            indexesInitialized = true;
        }
        finally {
            indexInitializationLock.Release();
        }
    }

    private async Task EnsureTextSearchIndexAsync(
        IMongoCollection<MongoDbSearchDocument> collection,
        CancellationToken cancellationToken)
    {
        using var existingIndexes = await collection.SearchIndexes.ListAsync(
            options.SearchIndexName,
            cancellationToken: cancellationToken);

        if (await existingIndexes.AnyAsync(cancellationToken)) {
            return;
        }

        var definition = new BsonDocument {
            {
                "mappings", new BsonDocument {
                    { "dynamic", false },
                    {
                        "fields", new BsonDocument {
                            { "title", CreateBm25StringMapping() },
                            { "summary", CreateBm25StringMapping() },
                            { "chunkText", CreateBm25StringMapping() },
                            { "category", CreateTokenMapping() },
                            { "source", CreateTokenMapping() },
                            { "publishedAt", new BsonDocument("type", "date") }
                        }
                    }
                }
            }
        };

        var model = new CreateSearchIndexModel(
            options.SearchIndexName,
            SearchIndexType.Search,
            definition);

        await collection.SearchIndexes.CreateOneAsync(model, cancellationToken);
    }

    private async Task EnsureVectorSearchIndexAsync(
        IMongoCollection<MongoDbSearchDocument> collection,
        CancellationToken cancellationToken)
    {
        using var existingIndexes = await collection.SearchIndexes.ListAsync(
            options.VectorSearchIndexName,
            cancellationToken: cancellationToken);

        if (await existingIndexes.AnyAsync(cancellationToken)) {
            return;
        }

        var definition = new BsonDocument(
            "fields",
            new BsonArray {
                new BsonDocument {
                    { "type", "vector" },
                    { "path", "embedding" },
                    { "numDimensions", embeddingService.GetModelInfo().Dimensions },
                    { "similarity", "cosine" }
                },
                CreateVectorFilterMapping("category"),
                CreateVectorFilterMapping("source"),
                CreateVectorFilterMapping("publishedAt")
            });

        var model = new CreateSearchIndexModel(
            options.VectorSearchIndexName,
            SearchIndexType.VectorSearch,
            definition);

        await collection.SearchIndexes.CreateOneAsync(model, cancellationToken);
    }

    private static BsonDocument CreateBm25StringMapping() => new() {
        { "type", "string" },
        { "similarity", new BsonDocument("type", "bm25") }
    };

    private static BsonDocument CreateTokenMapping() => new() {
        { "type", "token" },
        { "normalizer", "lowercase" }
    };

    private static BsonDocument CreateVectorFilterMapping(string path) => new() {
        { "type", "filter" },
        { "path", path }
    };

    private IMongoCollection<MongoDbSearchDocument> GetCollection()
    {
        return mongoClient
            .GetDatabase(options.DatabaseName)
            .GetCollection<MongoDbSearchDocument>(options.CollectionName);
    }

    private static MongoDbSearchDocument ToDocument(
        Article article,
        ArticleChunk chunk,
        ArticleChunkEmbedding embedding)
    {
        return new MongoDbSearchDocument {
            Id = chunk.Id,
            ArticleId = article.Id,
            ChunkId = chunk.Id,
            ChunkIndex = chunk.ChunkIndex,
            Title = article.Title,
            Summary = article.Summary,
            Author = article.Author,
            Source = article.Source,
            Url = article.Url,
            Category = article.Category,
            PublishedAt = article.PublishedAt?.UtcDateTime,
            IngestedAt = article.IngestedAt.UtcDateTime,
            ChunkText = chunk.Text,
            WordCount = chunk.WordCount,
            EmbeddingModel = embedding.Model,
            EmbeddingDimensions = embedding.Dimensions,
            Embedding = embedding.Vector.ToArray()
        };
    }

    private sealed record BuildDocumentsResult(
        int ArticleCount,
        int ChunkCount,
        int SkippedChunksWithoutEmbedding,
        IReadOnlyCollection<MongoDbSearchDocument> Documents);
}
