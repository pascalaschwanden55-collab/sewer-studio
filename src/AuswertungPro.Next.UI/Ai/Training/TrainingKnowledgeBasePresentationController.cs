using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseStatusPresentationUi(
    Action<int> SetSampleCount,
    Action<int> SetErrorCount,
    Action<int> SetNewCount,
    Action<int> SetEmbeddingCount,
    Action<int> SetCodesCovered,
    Action<string> SetLastUpdateText,
    Action<string> SetReadinessLabel,
    Action<Brush> SetReadinessBrush,
    Action<string> SetTopCodesText);

public sealed record TrainingKnowledgeBaseQualityPresentationUi(
    Action<string> SetCoverageGapsText,
    Action<int> SetCoverageGapsCount,
    Action<string> SetAccuracyText,
    Action<int> SetStaleSampleCount,
    Action<string> SetTrendText,
    Action<string> SetTrendDirection);

public static class TrainingKnowledgeBasePresentationController
{
    public static void ApplyStatus(
        TrainingKnowledgeBaseStatusPresentation presentation,
        TrainingKnowledgeBaseStatusPresentationUi ui)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetSampleCount(presentation.SampleCount);
        ui.SetErrorCount(presentation.ErrorCount);
        ui.SetNewCount(presentation.NewCount);
        ui.SetEmbeddingCount(presentation.EmbeddingCount);
        ui.SetCodesCovered(presentation.CodesCovered);
        ui.SetLastUpdateText(presentation.LastUpdateText);
        ui.SetReadinessLabel(presentation.ReadinessLabel);
        ui.SetReadinessBrush(presentation.ReadinessBrush);
        ui.SetTopCodesText(presentation.TopCodesText);
    }

    public static void ApplyQuality(
        TrainingKnowledgeBaseQualityPresentation presentation,
        TrainingKnowledgeBaseQualityPresentationUi ui)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetCoverageGapsText(presentation.CoverageGapsText);
        ui.SetCoverageGapsCount(presentation.CoverageGapsCount);
        ui.SetAccuracyText(presentation.AccuracyText);
        ui.SetStaleSampleCount(presentation.StaleSampleCount);
        ui.SetTrendText(presentation.TrendText);
        ui.SetTrendDirection(presentation.TrendDirection);
    }
}
