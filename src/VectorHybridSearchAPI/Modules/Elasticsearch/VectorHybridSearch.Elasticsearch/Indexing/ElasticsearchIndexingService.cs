using System.Net;
using System.Text;
using System.Text.Json;
using VectorHybridSearch.DataIngestion.Articles.Domain;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.DataIngestion.Articles.Repositories;
using VectorHybridSearch.Embeddings.Services;

namespace VectorHybridSearch.Elasticsearch.Indexing;

public sealed class ElasticsearchIndexingService(
    HttpClient httpClient,
    ElasticsearchOptions options,
    IEmbeddingService embeddingService,
    IArticleRepository articleRepository,
    IArticleChunkRepository articleChunkRepository,
    IArticleChunkEmbeddingRepository articleChunkEmbeddingRepository) : IElasticsearchIndexingService, IArticleSearchIndexWriter
{
    public string Source => "Elasticsearch";

    public async Task<ElasticsearchIndexingResult> RebuildIndexAsync(CancellationToken cancellationToken)
    {
        var documents = await BuildDocumentsAsync(cancellationToken);

        await DeleteIndexIfExistsAsync(cancellationToken);
        await CreateIndexAsync(cancellationToken);

        if (documents.Documents.Count > 0) {
            await BulkIndexAsync(documents.Documents, cancellationToken);
        }

        return new ElasticsearchIndexingResult(
            IndexName: options.IndexName,
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
        await EnsureIndexExistsAsync(cancellationToken);

        var documents = BuildDocumentsForArticle(
            article,
            chunks,
            embeddings,
            out var skippedChunksWithoutEmbedding);

        if (documents.Count > 0) {
            await BulkIndexAsync(documents, cancellationToken);
        }

        return new ArticleSearchIndexingResult(
            Source: Source,
            IndexedDocuments: documents.Count,
            SkippedChunksWithoutEmbedding: skippedChunksWithoutEmbedding);
    }

    private async Task<BuildDocumentsResult> BuildDocumentsAsync(CancellationToken cancellationToken)
    {
        var articles = await articleRepository.GetAllAsync(cancellationToken);
        var documents = new List<ElasticsearchSearchDocument>();
        var chunkCount = 0;
        var skippedChunksWithoutEmbedding = 0;

        foreach (var article in articles) {
            cancellationToken.ThrowIfCancellationRequested();

            var chunks = await articleChunkRepository.GetByArticleIdAsync(article.Id, cancellationToken);
            var embeddings = await articleChunkEmbeddingRepository.GetByArticleIdAsync(article.Id, cancellationToken);
            var embeddingsByChunkId = embeddings.ToDictionary(
                embedding => embedding.ChunkId,
                StringComparer.OrdinalIgnoreCase);

            chunkCount += chunks.Count;

            foreach (var chunk in chunks) {
                if (!embeddingsByChunkId.TryGetValue(chunk.Id, out var embedding)) {
                    skippedChunksWithoutEmbedding++;
                    continue;
                }

                documents.Add(ToDocument(article, chunk, embedding));
            }
        }

        return new BuildDocumentsResult(
            ArticleCount: articles.Count,
            ChunkCount: chunkCount,
            SkippedChunksWithoutEmbedding: skippedChunksWithoutEmbedding,
            Documents: documents);
    }

    private async Task DeleteIndexIfExistsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(ElasticsearchHttp.EscapeIndexName(options.IndexName), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) {
            return;
        }

        await ElasticsearchHttp.EnsureSuccessAsync(response, "delete Elasticsearch index", cancellationToken);
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ElasticsearchHttp.EscapeIndexName(options.IndexName));
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode) {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound) {
            await CreateIndexAsync(cancellationToken);
            return;
        }

        await ElasticsearchHttp.EnsureSuccessAsync(response, "check Elasticsearch index", cancellationToken);
    }

    private async Task CreateIndexAsync(CancellationToken cancellationToken)
    {
        var requestBody = new {
            settings = new {
                number_of_shards = 1,
                number_of_replicas = 0
            },
            mappings = new {
                properties = new Dictionary<string, object> {
                    ["id"] = new { type = "keyword" },
                    ["articleId"] = new { type = "keyword" },
                    ["chunkId"] = new { type = "keyword" },
                    ["chunkIndex"] = new { type = "integer" },
                    ["title"] = new { type = "text" },
                    ["summary"] = new { type = "text" },
                    ["author"] = new { type = "keyword" },
                    ["source"] = new { type = "keyword" },
                    ["url"] = new { type = "keyword" },
                    ["category"] = new { type = "keyword" },
                    ["publishedAt"] = new { type = "date" },
                    ["ingestedAt"] = new { type = "date" },
                    ["chunkText"] = new { type = "text" },
                    ["wordCount"] = new { type = "integer" },
                    ["embeddingModel"] = new { type = "keyword" },
                    ["embeddingDimensions"] = new { type = "integer" },
                    ["embedding"] = new {
                        type = "dense_vector",
                        dims = embeddingService.GetModelInfo().Dimensions,
                        index = true,
                        similarity = "cosine"
                    }
                }
            }
        };

        using var content = ElasticsearchHttp.CreateJsonContent(requestBody);
        using var response = await httpClient.PutAsync(ElasticsearchHttp.EscapeIndexName(options.IndexName), content, cancellationToken);

        await ElasticsearchHttp.EnsureSuccessAsync(response, "create Elasticsearch index", cancellationToken);
    }

    private async Task BulkIndexAsync(
        IReadOnlyCollection<ElasticsearchSearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var bulkPayload = new StringBuilder();

        foreach (var document in documents) {
            cancellationToken.ThrowIfCancellationRequested();

            var action = new {
                index = new {
                    _index = options.IndexName,
                    _id = document.Id
                }
            };

            bulkPayload.AppendLine(JsonSerializer.Serialize(action, ElasticsearchHttp.JsonOptions));
            bulkPayload.AppendLine(JsonSerializer.Serialize(document, ElasticsearchHttp.JsonOptions));
        }

        using var content = new StringContent(bulkPayload.ToString(), Encoding.UTF8, "application/x-ndjson");
        using var response = await httpClient.PostAsync("_bulk", content, cancellationToken);
        await ElasticsearchHttp.EnsureSuccessAsync(response, "bulk index Elasticsearch documents", cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var responseJson = JsonDocument.Parse(responseBody);

        if (responseJson.RootElement.TryGetProperty("errors", out var errors) && errors.GetBoolean()) {
            throw new InvalidOperationException("Elasticsearch bulk indexing completed with item-level errors.");
        }
    }

    private static IReadOnlyCollection<ElasticsearchSearchDocument> BuildDocumentsForArticle(
        Article article,
        IReadOnlyCollection<ArticleChunk> chunks,
        IReadOnlyCollection<ArticleChunkEmbedding> embeddings,
        out int skippedChunksWithoutEmbedding)
    {
        var documents = new List<ElasticsearchSearchDocument>();
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

    private static ElasticsearchSearchDocument ToDocument(
        Article article,
        ArticleChunk chunk,
        ArticleChunkEmbedding embedding)
    {
        return new ElasticsearchSearchDocument(
            Id: chunk.Id,
            ArticleId: article.Id,
            ChunkId: chunk.Id,
            ChunkIndex: chunk.ChunkIndex,
            Title: article.Title,
            Summary: article.Summary,
            Author: article.Author,
            Source: article.Source,
            Url: article.Url,
            Category: article.Category,
            PublishedAt: article.PublishedAt,
            IngestedAt: article.IngestedAt,
            ChunkText: chunk.Text,
            WordCount: chunk.WordCount,
            EmbeddingModel: embedding.Model,
            EmbeddingDimensions: embedding.Dimensions,
            Embedding: embedding.Vector);
    }

    private sealed record BuildDocumentsResult(
        int ArticleCount,
        int ChunkCount,
        int SkippedChunksWithoutEmbedding,
        IReadOnlyCollection<ElasticsearchSearchDocument> Documents);
}
