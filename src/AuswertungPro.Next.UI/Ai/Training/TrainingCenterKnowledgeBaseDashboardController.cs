using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai.Training;

internal sealed record TrainingCenterKnowledgeBaseDashboardUi(
    Action<int> SetSampleCount,
    Action<int> SetErrorCount,
    Action<int> SetNewCount,
    Action<int> SetEmbeddingCount,
    Action<int> SetCodesCovered,
    Action<string> SetLastUpdate,
    Action<string> SetReadinessLabel,
    Action<Brush> SetReadinessBrush,
    Action<string> SetTopCodesText,
    Action<string> SetCoverageGapsText,
    Action<int> SetCoverageGapsCount,
    Action<string> SetAccuracyText,
    Action<int> SetStaleSampleCount,
    Action<string> SetTrendText,
    Action<string> SetTrendDirection,
    Action<IReadOnlyList<double>> SetTrendSeries,
    Action<string> Log,
    Action<Action> OnUi);

/// <summary>
/// Laedt den Wissensdatenbank-Stand und uebertraegt die fertige Darstellung
/// auf das Trainingszentrum. Das ViewModel kennt dadurch keine Dashboard-Details.
/// </summary>
internal sealed class TrainingCenterKnowledgeBaseDashboardController
{
    private readonly IKnowledgeBaseDiagnosticsRunner _diagnostics;
    private readonly TrainingCenterKnowledgeBaseDashboardUi _ui;

    internal TrainingCenterKnowledgeBaseDashboardController(
        IKnowledgeBaseDiagnosticsRunner diagnostics,
        TrainingCenterKnowledgeBaseDashboardUi ui)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    internal Task RefreshStatusAsync()
    {
        return TrainingKnowledgeBaseStatusRefreshWorkflow.RunAsync(
            TrainingKnowledgeBaseStatusRefreshRequestFactory.Create(
                new TrainingKnowledgeBaseStatusRefreshRequestFactoryRequest(
                    topCodes => _diagnostics.ReadStatusAsync(topCodes),
                    ApplyStatus,
                    RefreshQualityAsync,
                    _ui.OnUi)));
    }

    internal Task RefreshQualityAsync()
    {
        return TrainingKnowledgeBaseQualityRefreshWorkflow.RunAsync(
            TrainingKnowledgeBaseQualityRefreshRequestFactory.CreateWithDefaults(
                new TrainingKnowledgeBaseQualityRefreshDefaultRequestFactoryRequest(
                    () => _diagnostics.ReadQualityAsync(),
                    ApplyQuality,
                    _ui.Log,
                    _ui.OnUi)));
    }

    internal void ApplyStatus(TrainingKnowledgeBaseStatusPresentation presentation)
    {
        TrainingKnowledgeBasePresentationController.ApplyStatus(
            presentation,
            new TrainingKnowledgeBaseStatusPresentationUi(
                _ui.SetSampleCount,
                _ui.SetErrorCount,
                _ui.SetNewCount,
                _ui.SetEmbeddingCount,
                _ui.SetCodesCovered,
                _ui.SetLastUpdate,
                _ui.SetReadinessLabel,
                _ui.SetReadinessBrush,
                _ui.SetTopCodesText));
    }

    internal void ApplyQuality(TrainingKnowledgeBaseQualityPresentation presentation)
    {
        TrainingKnowledgeBasePresentationController.ApplyQuality(
            presentation,
            new TrainingKnowledgeBaseQualityPresentationUi(
                _ui.SetCoverageGapsText,
                _ui.SetCoverageGapsCount,
                _ui.SetAccuracyText,
                _ui.SetStaleSampleCount,
                _ui.SetTrendText,
                _ui.SetTrendDirection));

        _ui.SetTrendSeries(presentation.TrendExactSeries ?? []);
    }
}
