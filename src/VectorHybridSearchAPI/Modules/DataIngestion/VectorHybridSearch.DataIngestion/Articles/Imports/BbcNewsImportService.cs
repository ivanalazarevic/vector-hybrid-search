using System.Text.Json;
using System.Text.Json.Serialization;
using VectorHybridSearch.DataIngestion.Articles.Results;
using VectorHybridSearch.DataIngestion.Articles.Services;
using VectorHybridSearch.Shared.Contracts.Articles;

namespace VectorHybridSearch.DataIngestion.Articles.Imports;

public sealed class BbcNewsImportService(IArticleIngestionService articleIngestionService) : IBbcNewsImportService
{
    private const int DefaultMaxArticles = 1000;
    private const int MaxAllowedArticles = 10000;

    public async Task<BbcNewsImportResult> ImportAsync(
        ImportBbcNewsRequest request,
        CancellationToken cancellationToken)
    {
        var filePath = request.FilePath?.Trim();
        if (string.IsNullOrWhiteSpace(filePath)) {
            throw new ArgumentException("File path is required.", nameof(request));
        }

        if (!File.Exists(filePath)) {
            throw new FileNotFoundException("BBC News dataset file was not found.", filePath);
        }

        if (!string.Equals(Path.GetExtension(filePath), ".jsonl", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("BBC News import currently supports .jsonl files only.", nameof(request));
        }

        var maxArticles = request.MaxArticles <= 0
            ? DefaultMaxArticles
            : Math.Min(request.MaxArticles, MaxAllowedArticles);

        var articles = new List<ArticleDto>();
        var readLines = 0;
        var splitName = Path.GetFileNameWithoutExtension(filePath);

        await foreach (var line in File.ReadLinesAsync(filePath, cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();

            if (articles.Count >= maxArticles) {
                break;
            }

            readLines++;

            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            var row = JsonSerializer.Deserialize<BbcNewsJsonLine>(line);
            if (string.IsNullOrWhiteSpace(row?.Text)) {
                continue;
            }

            articles.Add(new ArticleDto(
                Id: $"bbc-{splitName}-{readLines:D6}",
                Title: CreateTitle(row.Text),
                Content: row.Text,
                Summary: null,
                Author: null,
                Source: "BBC",
                Url: null,
                Category: row.LabelText,
                PublishedAt: null));
        }

        var ingestionResult = await articleIngestionService.IngestAsync(
            new IngestArticlesRequest(articles),
            cancellationToken);

        return new BbcNewsImportResult(
            FilePath: filePath,
            ReadLines: readLines,
            ImportedArticles: ingestionResult.Accepted,
            CreatedChunks: ingestionResult.CreatedChunks,
            CreatedEmbeddings: ingestionResult.CreatedEmbeddings,
            IndexingResults: ingestionResult.IndexingResults,
            RejectedIds: ingestionResult.RejectedIds);
    }

    private static string CreateTitle(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Take(12));
    }

    private sealed record BbcNewsJsonLine(
        [property: JsonPropertyName("text")]
        string Text,
        [property: JsonPropertyName("label")]
        int Label,
        [property: JsonPropertyName("label_text")]
        string LabelText);
}
