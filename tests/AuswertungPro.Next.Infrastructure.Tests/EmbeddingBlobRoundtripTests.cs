using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert den EINZIGEN Vektor-Persistenzpfad der KnowledgeBase (ToBlob/FromBlob) gegen stille
/// Korruption (Off-by-4-Byte, falsche Laenge). Rein deterministisch, kein Netz/Ollama.
/// </summary>
public sealed class EmbeddingBlobRoundtripTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(768)]
    public void ToBlob_FromBlob_IsLossless(int length)
    {
        var vector = new float[length];
        for (var i = 0; i < length; i++)
            vector[i] = (i % 2 == 0 ? 1f : -1f) * (i + 0.123456f) / 7f;

        var blob = EmbeddingService.ToBlob(vector);
        Assert.Equal(length * sizeof(float), blob.Length);

        var restored = EmbeddingService.FromBlob(blob);
        Assert.Equal(vector.Length, restored.Length);
        for (var i = 0; i < length; i++)
            Assert.Equal(BitConverter.SingleToInt32Bits(vector[i]), BitConverter.SingleToInt32Bits(restored[i]));
    }

    [Fact]
    public void FromBlob_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(EmbeddingService.FromBlob(Array.Empty<byte>()));
        Assert.Empty(EmbeddingService.FromBlob(null!));
    }

    [Fact]
    public void FromBlob_NonMultipleOfFour_ThrowsClearError()
    {
        Assert.Throws<ArgumentException>(() => EmbeddingService.FromBlob(new byte[] { 1, 2, 3 }));
        Assert.Throws<ArgumentException>(() => EmbeddingService.FromBlob(new byte[] { 1, 2, 3, 4, 5 }));
    }
}
