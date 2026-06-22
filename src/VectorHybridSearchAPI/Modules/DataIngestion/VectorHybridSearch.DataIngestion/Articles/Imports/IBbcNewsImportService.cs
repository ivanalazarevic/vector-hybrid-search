using VectorHybridSearch.DataIngestion.Articles.Results;
using VectorHybridSearch.Shared.Contracts.Articles;

namespace VectorHybridSearch.DataIngestion.Articles.Imports;

public interface
    IBbcNewsImportService
{
    Task<BbcNewsImportResult> ImportAsync(
        ImportBbcNewsRequest request,
        CancellationToken cancellationToken);
}
