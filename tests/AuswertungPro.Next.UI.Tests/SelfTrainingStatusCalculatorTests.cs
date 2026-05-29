using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus TrainingCenterViewModel extrahierte Self-Training-Status-/
/// Visualisierungsberechnung ab: Match-Rate-Prozente, Stage-Modell-Anzeige,
/// Level-Beschriftung und Code-Verteilungs-Inkrement. Reine Logik, kein UI.
/// </summary>
public sealed class SelfTrainingStatusCalculatorTests
{
    // --- ComputeMatchRatePercents ---

    [Fact]
    public void ComputeMatchRatePercents_verteilt_gleichmaessig()
    {
        var p = SelfTrainingStatusCalculator.ComputeMatchRatePercents(1, 1, 1, 1);

        Assert.Equal(0.25, p.Exact);
        Assert.Equal(0.25, p.Partial);
        Assert.Equal(0.25, p.Mismatch);
        Assert.Equal(0.25, p.NoFindings);
    }

    [Fact]
    public void ComputeMatchRatePercents_liefert_null_bei_keinen_faellen()
    {
        var p = SelfTrainingStatusCalculator.ComputeMatchRatePercents(0, 0, 0, 0);

        Assert.Equal(0, p.Exact);
        Assert.Equal(0, p.Partial);
        Assert.Equal(0, p.Mismatch);
        Assert.Equal(0, p.NoFindings);
    }

    [Fact]
    public void ComputeMatchRatePercents_rechnet_anteile()
    {
        var p = SelfTrainingStatusCalculator.ComputeMatchRatePercents(3, 1, 0, 0);

        Assert.Equal(0.75, p.Exact);
        Assert.Equal(0.25, p.Partial);
        Assert.Equal(0, p.Mismatch);
    }

    // --- ResolveActiveModel ---

    [Theory]
    [InlineData(SelfTrainingStage.BuildingTimeline, "PdfPig (CPU)", true)]
    [InlineData(SelfTrainingStage.ExtractingFrame, "ffmpeg (CPU)", true)]
    [InlineData(SelfTrainingStage.Comparing, "Deterministisch (CPU)", true)]
    [InlineData(SelfTrainingStage.Completed, "", false)]
    public void ResolveActiveModel_mappt_feste_stages(SelfTrainingStage stage, string label, bool active)
    {
        var (modelLabel, isActive) = SelfTrainingStatusCalculator.ResolveActiveModel(stage, "Qwen2.5-VL");

        Assert.Equal(label, modelLabel);
        Assert.Equal(active, isActive);
    }

    [Theory]
    [InlineData(SelfTrainingStage.Analyzing)]
    [InlineData(SelfTrainingStage.AssessingTechnique)]
    public void ResolveActiveModel_setzt_visionmodell_bei_gpu_stages(SelfTrainingStage stage)
    {
        var (modelLabel, isActive) = SelfTrainingStatusCalculator.ResolveActiveModel(stage, "Qwen2.5-VL");

        Assert.Equal("Qwen2.5-VL (GPU)", modelLabel);
        Assert.True(isActive);
    }

    // --- FormatLevel ---

    [Theory]
    [InlineData(MatchLevel.ExactMatch, "EXACT")]
    [InlineData(MatchLevel.PartialMatch, "PARTIAL")]
    [InlineData(MatchLevel.Mismatch, "MISMATCH")]
    [InlineData(MatchLevel.NoFindings, "NO_FINDINGS")]
    public void FormatLevel_liefert_label(MatchLevel level, string expected)
    {
        Assert.Equal(expected, SelfTrainingStatusCalculator.FormatLevel(level));
    }

    // --- ApplyMatch ---

    [Fact]
    public void ApplyMatch_erhoeht_total_und_passenden_zaehler()
    {
        var entry = new CodeDistributionEntry { Code = "BAB" };

        SelfTrainingStatusCalculator.ApplyMatch(entry, MatchLevel.ExactMatch);
        SelfTrainingStatusCalculator.ApplyMatch(entry, MatchLevel.PartialMatch);
        SelfTrainingStatusCalculator.ApplyMatch(entry, MatchLevel.ExactMatch);

        Assert.Equal(3, entry.Total);
        Assert.Equal(2, entry.Exact);
        Assert.Equal(1, entry.Partial);
        Assert.Equal(0, entry.Mismatch);
        Assert.Equal(0, entry.NoFindings);
    }

    [Fact]
    public void ApplyMatch_zaehlt_mismatch_und_nofindings()
    {
        var entry = new CodeDistributionEntry { Code = "BBA" };

        SelfTrainingStatusCalculator.ApplyMatch(entry, MatchLevel.Mismatch);
        SelfTrainingStatusCalculator.ApplyMatch(entry, MatchLevel.NoFindings);

        Assert.Equal(2, entry.Total);
        Assert.Equal(1, entry.Mismatch);
        Assert.Equal(1, entry.NoFindings);
    }
}
