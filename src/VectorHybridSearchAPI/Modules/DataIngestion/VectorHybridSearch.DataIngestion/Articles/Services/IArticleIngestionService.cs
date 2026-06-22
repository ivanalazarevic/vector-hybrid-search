using VectorHybridSearch.DataIngestion.Articles.Results;
using VectorHybridSearch.Shared.Contracts.Articles;

namespace VectorHybridSearch.DataIngestion.Articles.Services;

public interface IArticleIngestionService
{
    Task<ArticleIngestionResult> IngestAsync(
        IngestArticlesRequest request,
        CancellationToken cancellationToken);
}
