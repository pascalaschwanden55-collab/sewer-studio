using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert den qualitaetsbewussten Retrieval-Filter (RetrievalService.RankAndFilter).
/// Deterministisch, ohne Ollama/DB: synthetische Vektoren + explizite Qualitaetsstufen.
/// Policy-Ziel: Green bevorzugen, Yellow zulassen (niedriger gewichtet), Red standardmaessig
/// ausschliessen und nur als kontrollierter Fallback (immer zuletzt) zulassen.
/// </summary>
public sealed class RetrievalQualityFilterTests
{
    private static readonly float[] Query = { 1f, 0f, 0f };

    // Cosine zu Query=[1,0,0]: [1,0,0]->1.0  [1,1,0]->0.7071  [1,2,0]->0.4472
    private static (string, float[], SampleRecord?) Cand(string id, float[] vec, string quality)
        => (id, vec, new SampleRecord(id, "case", "BAB", "Riss", 0, 0, quality));

    private static IReadOnlyList<RetrievalResult> Rank(
        IReadOnlyList<(string, float[], SampleRecord?)> cands, int topK, RetrievalQualityPolicy policy, out int mismatch)
        => RetrievalService.RankAndFilter(Query, cands, topK, policy, out mismatch);

    [Fact]
    public void DefaultPolicy_RanksByCosine_HigherCosineYellowBeatsLowerCosineGreen()
    {
        // Default = reiner Filter (Red raus), KEINE Green-Bevorzugung -> Cosine entscheidet.
        var cands = new[]
        {
            Cand("g", new[] { 1f, 1f, 0f }, "Green"),  // cos 0.7071
            Cand("y", new[] { 1f, 0f, 0f }, "Yellow"), // cos 1.0
        };

        var res = Rank(cands, topK: 2, RetrievalQualityPolicy.Default, out _);

        Assert.Equal("y", res[0].Sample.SampleId);     // hoehere Cosine gewinnt, kein Green-Bias
        Assert.Equal("g", res[1].Sample.SampleId);
    }

    [Fact]
    public void GreenPreferringPolicy_OutranksEqualCosineYellow()
    {
        // Optionaler Knopf: wer Green bevorzugen WILL, setzt YellowWeight < GreenWeight.
        var cands = new[]
        {
            Cand("y", new[] { 1f, 0f, 0f }, "Yellow"),
            Cand("g", new[] { 1f, 0f, 0f }, "Green"),
        };
        var policy = RetrievalQualityPolicy.Default with { YellowWeight = 0.6 };

        var res = Rank(cands, topK: 2, policy, out _);

        Assert.Equal("g", res[0].Sample.SampleId);
        Assert.Equal("y", res[1].Sample.SampleId);
    }

    [Fact]
    public void Red_ExcludedByDefault_WhenEnoughPrimary()
    {
        var cands = new[]
        {
            Cand("g", new[] { 1f, 0f, 0f }, "Green"),
            Cand("y", new[] { 1f, 0f, 0f }, "Yellow"),
            Cand("r", new[] { 1f, 0f, 0f }, "Red"),
        };

        var res = Rank(cands, topK: 2, RetrievalQualityPolicy.Default, out _);

        Assert.Equal(2, res.Count);
        Assert.DoesNotContain(res, x => x.Sample.SampleId == "r");
    }

    [Fact]
    public void Red_FallbackFillsLeftoverSlots_AndIsDemoted()
    {
        var cands = new[]
        {
            Cand("g", new[] { 1f, 1f, 0f }, "Green"), // cos 0.7071
            Cand("r", new[] { 1f, 0f, 0f }, "Red"),   // cos 1.0
        };

        var res = Rank(cands, topK: 2, RetrievalQualityPolicy.Default, out _);

        Assert.Equal(2, res.Count);
        Assert.Equal("g", res[0].Sample.SampleId);     // Green zuerst, trotz niedrigerer Cosine
        Assert.Equal("r", res[1].Sample.SampleId);     // Red nur als Fallback, hinten
        Assert.True(res[1].Score < res[0].Score);      // Red ist abgewertet
    }

    [Fact]
    public void Red_NotReturned_WhenFallbackDisabled()
    {
        var cands = new[]
        {
            Cand("g", new[] { 1f, 1f, 0f }, "Green"),
            Cand("r", new[] { 1f, 0f, 0f }, "Red"),
        };
        var policy = RetrievalQualityPolicy.Default with { AllowRedFallback = false };

        var res = Rank(cands, topK: 2, policy, out _);

        Assert.Single(res);
        Assert.Equal("g", res[0].Sample.SampleId);
    }

    [Fact]
    public void UnknownQuality_TreatedAsAcceptable_NotExcludedLikeRed()
    {
        // Leere/unbekannte Stufe gilt als Yellow -> eingeschlossen; Red wird ausgeschlossen.
        var cands = new[]
        {
            Cand("u", new[] { 1f, 0f, 0f }, ""),      // unbekannt -> akzeptabel
            Cand("r", new[] { 1f, 0f, 0f }, "Red"),   // ausgeschlossen
        };

        // topK = 1: der eine Slot geht an "u"; Red bleibt ausgeschlossen (kein Fallback noetig).
        var res = Rank(cands, topK: 1, RetrievalQualityPolicy.Default, out _);

        Assert.Single(res);
        Assert.Equal("u", res[0].Sample.SampleId);
    }

    [Fact]
    public void StableTopK_TieBreakBySampleId()
    {
        var cands = new[]
        {
            Cand("b", new[] { 1f, 0f, 0f }, "Yellow"),
            Cand("a", new[] { 1f, 0f, 0f }, "Yellow"),
        };

        var res = Rank(cands, topK: 2, RetrievalQualityPolicy.Default, out _);

        Assert.Equal("a", res[0].Sample.SampleId);
        Assert.Equal("b", res[1].Sample.SampleId);
    }

    [Fact]
    public void DimensionMismatch_CountedAndExcluded()
    {
        var cands = new[]
        {
            Cand("ok", new[] { 1f, 0f, 0f }, "Green"),
            Cand("bad", new[] { 1f, 0f }, "Green"),    // falsche Dimension
        };

        var res = Rank(cands, topK: 5, RetrievalQualityPolicy.Default, out var mismatch);

        Assert.Single(res);
        Assert.Equal("ok", res[0].Sample.SampleId);
        Assert.Equal(1, mismatch);
    }

    [Fact]
    public void RespectsTopK()
    {
        var cands = new List<(string, float[], SampleRecord?)>();
        for (var i = 0; i < 5; i++)
            cands.Add(Cand($"g{i}", new[] { 1f, 0f, 0f }, "Green"));

        var res = Rank(cands, topK: 3, RetrievalQualityPolicy.Default, out _);

        Assert.Equal(3, res.Count);
    }

    [Fact]
    public void EmptyCandidates_ReturnsEmpty()
    {
        var res = Rank(new (string, float[], SampleRecord?)[0], topK: 5, RetrievalQualityPolicy.Default, out var mismatch);

        Assert.Empty(res);
        Assert.Equal(0, mismatch);
    }

    [Fact]
    public void NullSample_Skipped()
    {
        var cands = new (string, float[], SampleRecord?)[]
        {
            ("x", new[] { 1f, 0f, 0f }, null),
            Cand("g", new[] { 1f, 0f, 0f }, "Green"),
        };

        var res = Rank(cands, topK: 5, RetrievalQualityPolicy.Default, out _);

        Assert.Single(res);
        Assert.Equal("g", res[0].Sample.SampleId);
    }
}
