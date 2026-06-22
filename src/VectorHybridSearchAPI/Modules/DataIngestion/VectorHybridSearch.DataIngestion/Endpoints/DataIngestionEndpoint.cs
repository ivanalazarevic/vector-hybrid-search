using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VectorHybridSearch.DataIngestion.Articles.Imports;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.DataIngestion.Articles.Repositories;
using VectorHybridSearch.DataIngestion.Articles.Services;
using VectorHybridSearch.Shared.Api.Endpoints;
using VectorHybridSearch.Shared.Contracts.Articles;

namespace VectorHybridSearch.DataIngestion.Endpoints;

public sealed class DataIngestionEndpoint : IEndpoint
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion")
            .WithTags("Data ingestion");

        group.MapPost("/articles", async (
            IngestArticlesRequest request,
            IArticleIngestionService ingestionService,
            CancellationToken cancellationToken) => {
            try {
                var result = await ingestionService.IngestAsync(request, cancellationToken);

                var response = new IngestArticlesResponse(
                    Received: result.Received,
                    Accepted: result.Accepted,
                    CreatedChunks: result.CreatedChunks,
                    CreatedEmbeddings: result.CreatedEmbeddings,
                    IndexingResults: MapIndexingResults(result.IndexingResults),
                    RejectedIds: result.RejectedIds,
                    Status: "Articles, chunks, and placeholder embeddings stored in memory and written to configured search indexes.");

                return Results.Accepted($"/api/ingestion/jobs/manual-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", response);
            }
            catch (HttpRequestException exception) {
                return Results.Problem(
                    title: "A configured search index is not reachable.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException exception) {
                return Results.Problem(
                    title: "Search indexing failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapPost("/bbc-news/import", async (
            ImportBbcNewsRequest request,
            IBbcNewsImportService bbcNewsImportService,
            CancellationToken cancellationToken) => {
            try {
                var result = await bbcNewsImportService.ImportAsync(request, cancellationToken);

                var response = new ImportBbcNewsResponse(
                    FilePath: result.FilePath,
                    ReadLines: result.ReadLines,
                    ImportedArticles: result.ImportedArticles,
                    CreatedChunks: result.CreatedChunks,
                    CreatedEmbeddings: result.CreatedEmbeddings,
                    IndexingResults: MapIndexingResults(result.IndexingResults),
                    RejectedIds: result.RejectedIds,
                    Status: "BBC News articles imported into memory with chunks and placeholder embeddings, then written to configured search indexes.");

                return Results.Accepted("/api/ingestion/articles", response);
            }
            catch (HttpRequestException exception) {
                return Results.Problem(
                    title: "A configured search index is not reachable.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException exception) {
                return Results.Problem(
                    title: "Search indexing failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (FileNotFoundException exception) {
                return Results.NotFound(new {
                    Error = exception.Message,
                    exception.FileName
                });
            }
            catch (ArgumentException exception) {
                return Results.BadRequest(new {
                    Error = exception.Message
                });
            }
            catch (JsonException exception) {
                return Results.BadRequest(new {
                    Error = "The dataset file contains invalid JSONL content.",
                    exception.LineNumber,
                    exception.BytePositionInLine
                });
            }
        });

        group.MapGet("/articles", async (
            IArticleRepository articleRepository,
            CancellationToken cancellationToken) => {
            var articles = await articleRepository.GetAllAsync(cancellationToken);

            var response = articles
                .Select(article => new ArticleDto(
                    Id: article.Id,
                    Title: article.Title,
                    Content: article.Content,
                    Summary: article.Summary,
                    Author: article.Author,
                    Source: article.Source,
                    Url: article.Url,
                    Category: article.Category,
                    PublishedAt: article.PublishedAt))
                .ToArray();

            return Results.Ok(response);
        });

        group.MapGet("/articles/{articleId}/chunks", async (
            string articleId,
            IArticleChunkRepository articleChunkRepository,
            CancellationToken cancellationToken) => {
            var chunks = await articleChunkRepository.GetByArticleIdAsync(articleId, cancellationToken);

            var response = chunks
                .Select(chunk => new ArticleChunkDto(
                    Id: chunk.Id,
                    ArticleId: chunk.ArticleId,
                    ChunkIndex: chunk.ChunkIndex,
                    Text: chunk.Text,
                    WordCount: chunk.WordCount))
                .ToArray();

            return Results.Ok(response);
        });

        group.MapGet("/articles/{articleId}/embeddings", async (
            string articleId,
            IArticleChunkEmbeddingRepository articleChunkEmbeddingRepository,
            CancellationToken cancellationToken) => {
            var embeddings = await articleChunkEmbeddingRepository.GetByArticleIdAsync(articleId, cancellationToken);

            var response = embeddings
                .Select(embedding => new ArticleChunkEmbeddingDto(
                    Id: embedding.Id,
                    ArticleId: embedding.ArticleId,
                    ChunkId: embedding.ChunkId,
                    ChunkIndex: embedding.ChunkIndex,
                    Model: embedding.Model,
                    Dimensions: embedding.Dimensions,
                    Vector: embedding.Vector,
                    GeneratedAt: embedding.GeneratedAt))
                .ToArray();

            return Results.Ok(response);
        });

        group.MapGet("/articles/{articleId}/chunks/{chunkId}/embedding", async (
            string articleId,
            string chunkId,
            IArticleChunkEmbeddingRepository articleChunkEmbeddingRepository,
            CancellationToken cancellationToken) => {
            var embedding = await articleChunkEmbeddingRepository.GetByChunkIdAsync(chunkId, cancellationToken);

            if (embedding is null || !string.Equals(embedding.ArticleId, articleId, StringComparison.OrdinalIgnoreCase)) {
                return Results.NotFound();
            }

            var response = new ArticleChunkEmbeddingDto(
                Id: embedding.Id,
                ArticleId: embedding.ArticleId,
                ChunkId: embedding.ChunkId,
                ChunkIndex: embedding.ChunkIndex,
                Model: embedding.Model,
                Dimensions: embedding.Dimensions,
                Vector: embedding.Vector,
                GeneratedAt: embedding.GeneratedAt);

            return Results.Ok(response);
        });
    }

    private static IReadOnlyCollection<SearchIndexingResultDto> MapIndexingResults(
        IReadOnlyCollection<ArticleSearchIndexingResult> indexingResults)
    {
        return indexingResults
            .Select(result => new SearchIndexingResultDto(
                Source: result.Source,
                IndexedDocuments: result.IndexedDocuments,
                SkippedChunksWithoutEmbedding: result.SkippedChunksWithoutEmbedding))
            .ToArray();
    }
}
