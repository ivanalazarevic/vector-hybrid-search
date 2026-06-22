using System.Security.Cryptography;
using System.Text;

namespace VectorHybridSearch.Embeddings.Services;

public sealed class PlaceholderEmbeddingService : IEmbeddingService
{
    private const string Provider = "placeholder";
    private const string Model = "placeholder-deterministic-384";
    private const int Dimensions = 384;

    public EmbeddingModelInfo GetModelInfo()
    {
        return new EmbeddingModelInfo(
            Provider: Provider,
            Model: Model,
            Dimensions: Dimensions,
            IsPlaceholder: true);
    }

    public Task<EmbeddingResult> GenerateAsync(
        string input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input)) {
            throw new ArgumentException("Embedding input is required.", nameof(input));
        }

        var vector = CreateDeterministicVector(input);

        var result = new EmbeddingResult(
            Model: Model,
            Dimensions: Dimensions,
            Vector: vector,
            GeneratedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    private static float[] CreateDeterministicVector(string input)
    {
        var vector = new float[Dimensions];
        var seed = Encoding.UTF8.GetBytes(input.Trim());
        var offset = 0;
        var blockIndex = 0;

        while (offset < vector.Length) {
            var blockInput = Combine(seed, BitConverter.GetBytes(blockIndex));
            var hash = SHA256.HashData(blockInput);

            for (var hashIndex = 0; hashIndex < hash.Length && offset < vector.Length; hashIndex += 4) {
                var value = BitConverter.ToUInt32(hash, hashIndex);
                vector[offset] = (value / (float)uint.MaxValue * 2f) - 1f;
                offset++;
            }

            blockIndex++;
        }

        Normalize(vector);
        return vector;
    }

    private static byte[] Combine(byte[] left, byte[] right)
    {
        var combined = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, combined, 0, left.Length);
        Buffer.BlockCopy(right, 0, combined, left.Length, right.Length);
        return combined;
    }

    private static void Normalize(float[] vector)
    {
        var sum = 0d;
        foreach (var value in vector) {
            sum += value * value;
        }

        var magnitude = Math.Sqrt(sum);
        if (magnitude == 0d) {
            return;
        }

        for (var index = 0; index < vector.Length; index++) {
            vector[index] = (float)(vector[index] / magnitude);
        }
    }
}
