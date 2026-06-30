using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunCompletionControllerTests
{
    [Fact]
    public void Apply_loggt_abschluss_setzt_status_und_haengt_few_shot_hinweis_an()
    {
        var logLines = new List<string>();
        var statusText = "";

        SelfTrainingRunCompletionController.Apply(
            Result(exact: 2, partial: 1, samplesGenerated: 2),
            logLines.Add,
            value => statusText = value);

        Assert.Contains("--- Selbsttraining abgeschlossen ---", logLines);
        Assert.EndsWith("Fuer Few-Shot-Export: Tab 'Samples' \u2192 'Export Approved'", logLines[^1]);
        Assert.Equal("Fertig! 2/3 ExactMatch, 2 Samples in 00:01", statusText);
    }

    [Fact]
    public void Apply_loggt_keinen_few_shot_hinweis_ohne_exact_matches()
    {
        var logLines = new List<string>();

        SelfTrainingRunCompletionController.Apply(
            Result(exact: 0, partial: 1, samplesGenerated: 0),
            logLines.Add,
            _ => { });

        Assert.DoesNotContain(logLines, line => line.Contains("Few-Shot-Export", StringComparison.Ordinal));
    }

    private static SelfTrainingResult Result(
        int exact,
        int partial,
        int samplesGenerated)
        => new(
            "H-001",
            TotalEntries: exact + partial,
            ExactMatches: exact,
            PartialMatches: partial,
            Mismatches: 0,
            NoFindings: 0,
            OverallTechnique: null,
            Duration: TimeSpan.FromSeconds(1),
            SamplesGenerated: samplesGenerated);
}
