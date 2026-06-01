using System.Reflection;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert das Herzstueck des Few-Shot-Retrievals: die Cosine-Similarity. Deterministisch,
/// ohne Ollama/Netz. Die Methode ist private static – Zugriff bewusst via Reflection als
/// Regressions-Anker (kein Produktivcode-Eingriff fuer den Test).
/// </summary>
public sealed class RetrievalCosineTests
{
    private static readonly MethodInfo CosineMethod =
        typeof(RetrievalService).GetMethod("CosineSimilarity", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("RetrievalService.CosineSimilarity nicht gefunden (umbenannt?).");

    private static double Cosine(float[] a, float[] b)
        => (double)CosineMethod.Invoke(null, new object[] { a, b })!;

    [Fact]
    public void CosineSimilarity_ReturnsExpectedValues()
    {
        // Identisch -> 1
        Assert.Equal(1.0, Cosine(new[] { 1f, 0f, 0f }, new[] { 1f, 0f, 0f }), 6);
        // Gleiche Richtung, andere Magnitude -> 1 (normalisiert)
        Assert.Equal(1.0, Cosine(new[] { 1f, 2f, 3f }, new[] { 2f, 4f, 6f }), 6);
        // Orthogonal -> 0
        Assert.Equal(0.0, Cosine(new[] { 1f, 0f }, new[] { 0f, 1f }), 6);
        // Gegensaetzlich -> -1
        Assert.Equal(-1.0, Cosine(new[] { 1f, 0f }, new[] { -1f, 0f }), 6);
    }

    [Fact]
    public void CosineSimilarity_GuardsZeroVectorAndLengthMismatch()
    {
        // Nullvektor -> 0 (Division-durch-Null-Schutz, kein NaN)
        Assert.Equal(0.0, Cosine(new[] { 0f, 0f }, new[] { 1f, 1f }), 6);
        // Laengen-Mismatch -> 0 (kein Crash)
        Assert.Equal(0.0, Cosine(new[] { 1f, 2f, 3f }, new[] { 1f, 2f }), 6);
    }
}
