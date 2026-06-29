using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunPresentationBuilderTests
{
    [Fact]
    public void BuildCompletion_maps_result_to_log_lines_and_status()
    {
        var result = Result(
            totalEntries: 8,
            exact: 3,
            partial: 2,
            mismatch: 1,
            noFindings: 2,
            samples: 4,
            technique: new TechniqueAssessment(
                OsdReadable: true,
                OsdDeltaMeters: 0.1,
                LightingQuality: "Gut",
                SharpnessQuality: "Mittel",
                CenteringQuality: null,
                OverallGrade: "B",
                MeanLuminance: 127,
                LaplacianVariance: 42));

        var presentation = SelfTrainingRunPresentationBuilder.BuildCompletion(result);

        Assert.Equal(
            new[]
            {
                "--- Selbsttraining abgeschlossen ---",
                "  Dauer: 03:05",
                "  Eintraege: 8 gesamt",
                "  ExactMatch: 3 | PartialMatch: 2",
                "  Mismatch: 1 | NoFindings: 2",
                "  Samples erzeugt: 4",
                "  Technik: B (Licht=Gut, Schaerfe=Mittel)"
            },
            presentation.LogLines);
        Assert.Equal("Fertig! 3/8 ExactMatch, 4 Samples in 03:05", presentation.StatusText);
    }

    [Fact]
    public void BuildFewShotExportHint_only_returns_hint_when_exact_matches_exist()
    {
        Assert.Equal(
            "2 ExactMatch-Samples erzeugt. Fuer Few-Shot-Export: Tab 'Samples' \u2192 'Export Approved'",
            SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(Result(exact: 2)));
        Assert.Null(SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(Result(exact: 0)));
    }

    private static SelfTrainingResult Result(
        int totalEntries = 2,
        int exact = 1,
        int partial = 0,
        int mismatch = 0,
        int noFindings = 0,
        int samples = 1,
        TechniqueAssessment? technique = null)
        => new(
            "H-001",
            totalEntries,
            exact,
            partial,
            mismatch,
            noFindings,
            technique,
            TimeSpan.FromSeconds(185),
            samples);
}
