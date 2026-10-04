using VectorHybridSearch.Embeddings.Services;

namespace VectorHybridSearch.Tests.Embeddings;

public sealed class PlaceholderEmbeddingServiceTests
{
    // Both engines size their vector index from GetModelInfo().Dimensions, so generated vectors must match it.
    [Fact]
    public async Task GenerateAsync_ReturnsVectorsWithTheDimensionsReportedByGetModelInfo()
    {
        var service = new PlaceholderEmbeddingService();

        var embedding = await service.GenerateAsync("interest rates", CancellationToken.None);

        Assert.Equal(service.GetModelInfo().Dimensions, embedding.Dimensions);
        Assert.Equal(service.GetModelInfo().Dimensions, embedding.Vector.Count);
    }
}
