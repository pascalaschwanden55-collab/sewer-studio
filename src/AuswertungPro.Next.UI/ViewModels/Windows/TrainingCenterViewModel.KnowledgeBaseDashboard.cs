using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    private TrainingCenterKnowledgeBaseDashboardController CreateKnowledgeBaseDashboard(
        IKnowledgeBaseDiagnosticsRunner diagnostics)
        => new(
            diagnostics,
            _selfTrainingHistory,
            new TrainingCenterKnowledgeBaseDashboardUi(
                value => KbSampleCount = value,
                value => KbErrorCount = value,
                value => KbNewCount = value,
                value => KbEmbeddingCount = value,
                value => KbCodesCovered = value,
                value => KbLastUpdate = value,
                value => KbReadinessLabel = value,
                value => KbReadinessBrush = value,
                value => KbTopCodesText = value,
                value => KbCoverageGapsText = value,
                value => KbCoverageGapsCount = value,
                value => KbAccuracyText = value,
                value => KbStaleSampleCount = value,
                value => KbTrendText = value,
                value => KbTrendDirection = value,
                value => KbTrendSeries = value,
                Log,
                OnUi));
}
