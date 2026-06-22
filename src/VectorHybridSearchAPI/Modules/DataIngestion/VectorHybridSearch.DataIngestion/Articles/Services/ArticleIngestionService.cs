using System.Text.RegularExpressions;
using VectorHybridSearch.DataIngestion.Articles.Domain;
using VectorHybridSearch.DataIngestion.Articles.Indexing;
using VectorHybridSearch.DataIngestion.Articles.Repositories;
using VectorHybridSearch.DataIngestion.Articles.Results;
using VectorHybridSearch.Shared.Contracts.Articles;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public sealed class ArticleIngestionService(
    IArticleRepository articleRepository,
    IArticleChunkRepository articleChunkRepository,
    IArticleChunkEmbeddingRepository articleChunkEmbeddingRepository,
    IArticleChunkingService articleChunkingService,
    IArticleEmbeddingService articleEmbeddingService,
    IEnumerable<IArticleSearchIndexWriter> searchIndexWriters) : IArticleIngestionService
{
    public async Task<ArticleIngestionResult> IngestAsync(
        IngestArticlesRequest request,
        CancellationToken cancellationToken)
    {
        var incomingArticles = request.Articles;
        var validArticles = new List<Article>();
        var articleChunks = new Dictionary<string, IReadOnlyCollection<ArticleChunk>>(StringComparer.OrdinalIgnoreCase);
        var createdEmbeddings = 0;
        var indexingResultsBySource = new Dictionary<string, ArticleSearchIndexingResult>(StringComparer.OrdinalIgnoreCase);
        var rejectedIds = new List<string>();

        foreach (var article in incomingArticles) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateArticle(article, out var normalizedArticle)) {
                rejectedIds.Add(string.IsNullOrWhiteSpace(article.Id) ? "<missing-id>" : article.Id);
                continue;
            }

            validArticles.Add(normalizedArticle);
            articleChunks[normalizedArticle.Id] = articleChunkingService.CreateChunks(normalizedArticle);
        }

        if (validArticles.Count > 0) {
            await articleRepository.UpsertManyAsync(validArticles, cancellationToken);

            foreach (var article in validArticles) {
                var chunks = articleChunks[article.Id];

                await articleChunkRepository.UpsertForArticleAsync(
                    article.Id,
                    chunks,
                    cancellationToken);

                var embeddings = await articleEmbeddingService.GenerateForChunksAsync(
                    chunks,
                    cancellationToken);

                await articleChunkEmbeddingRepository.UpsertForArticleAsync(
                    article.Id,
                    embeddings,
                    cancellationToken);

                createdEmbeddings += embeddings.Count;

                foreach (var searchIndexWriter in searchIndexWriters) {
                    var indexingResult = await searchIndexWriter.IndexArticleAsync(
                        article,
                        chunks,
                        embeddings,
                        cancellationToken);

                    MergeIndexingResult(indexingResultsBySource, indexingResult);
                }
            }
        }

        return new ArticleIngestionResult(
            Received: incomingArticles.Count,
            Accepted: validArticles.Count,
            CreatedChunks: articleChunks.Values.Sum(chunks => chunks.Count),
            CreatedEmbeddings: createdEmbeddings,
            IndexingResults: indexingResultsBySource.Values.OrderBy(result => result.Source).ToArray(),
            RejectedIds: rejectedIds);
    }

    private static void MergeIndexingResult(
        IDictionary<string, ArticleSearchIndexingResult> indexingResultsBySource,
        ArticleSearchIndexingResult indexingResult)
    {
        if (!indexingResultsBySource.TryGetValue(indexingResult.Source, out var existingResult)) {
            indexingResultsBySource[indexingResult.Source] = indexingResult;
            return;
        }

        indexingResultsBySource[indexingResult.Source] = existingResult with {
            IndexedDocuments = existingResult.IndexedDocuments + indexingResult.IndexedDocuments,
            SkippedChunksWithoutEmbedding = existingResult.SkippedChunksWithoutEmbedding
                                            + indexingResult.SkippedChunksWithoutEmbedding
        };
    }

    private static bool TryCreateArticle(ArticleDto article, out Article normalizedArticle)
    {
        var id = NormalizeRequiredText(article.Id);
        var title = NormalizeRequiredText(article.Title);
        var content = NormalizeRequiredText(article.Content);

        if (id is null || title is null || content is null) {
            normalizedArticle = default!;
            return false;
        }

        normalizedArticle = new Article(
            Id: id,
            Title: title,
            Content: content,
            Summary: NormalizeOptionalText(article.Summary),
            Author: NormalizeOptionalText(article.Author),
            Source: NormalizeOptionalText(article.Source),
            Url: NormalizeOptionalText(article.Url),
            Category: NormalizeOptionalText(article.Category),
            PublishedAt: article.PublishedAt,
            IngestedAt: DateTimeOffset.UtcNow);

        return true;
    }

    private static string? NormalizeRequiredText(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
