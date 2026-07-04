using System.Windows.Media;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBasePresentationControllerTests
{
    [Fact]
    public void ApplyStatus_setzt_alle_status_felder()
    {
        var brush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
        var presentation = new TrainingKnowledgeBaseStatusPresentation(
            SampleCount: 42,
            ErrorCount: 2,
            NewCount: 3,
            EmbeddingCount: 40,
            CodesCovered: 7,
            LastUpdateText: "30.06.2026 08:15",
            ReadinessLabel: "Bereit",
            ReadinessBrush: brush,
            TopCodesText: "BAA: 4 Samples");
        var calls = new List<string>();
        Brush? appliedBrush = null;

        TrainingKnowledgeBasePresentationController.ApplyStatus(
            presentation,
            new TrainingKnowledgeBaseStatusPresentationUi(
                value => calls.Add($"samples:{value}"),
                value => calls.Add($"errors:{value}"),
                value => calls.Add($"new:{value}"),
                value => calls.Add($"embeddings:{value}"),
                value => calls.Add($"codes:{value}"),
                value => calls.Add($"last:{value}"),
                value => calls.Add($"label:{value}"),
                value => appliedBrush = value,
                value => calls.Add($"top:{value}")));

        Assert.Equal(
            [
                "samples:42",
                "errors:2",
                "new:3",
                "embeddings:40",
                "codes:7",
                "last:30.06.2026 08:15",
                "label:Bereit",
                "top:BAA: 4 Samples"
            ],
            calls);
        Assert.Same(brush, appliedBrush);
    }

    [Fact]
    public void ApplyQuality_setzt_alle_quality_felder()
    {
        var presentation = new TrainingKnowledgeBaseQualityPresentation(
            CoverageGapsText: "BAA fehlt",
            CoverageGapsCount: 1,
            AccuracyText: "80%",
            StaleSampleCount: 3,
            TrendText: "Trend",
            TrendDirection: "\u2191",
            LogLines: []);
        var calls = new List<string>();

        TrainingKnowledgeBasePresentationController.ApplyQuality(
            presentation,
            new TrainingKnowledgeBaseQualityPresentationUi(
                value => calls.Add($"gaps-text:{value}"),
                value => calls.Add($"gaps:{value}"),
                value => calls.Add($"accuracy:{value}"),
                value => calls.Add($"stale:{value}"),
                value => calls.Add($"trend:{value}"),
                value => calls.Add($"direction:{value}")));

        Assert.Equal(
            [
                "gaps-text:BAA fehlt",
                "gaps:1",
                "accuracy:80%",
                "stale:3",
                "trend:Trend",
                "direction:\u2191"
            ],
            calls);
    }
}
