using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Rückfall nur für den parameterlosen WPF-/Designer-Konstruktor. Im normalen Programmstart
/// kommen dieselben Dienste aus dem zentralen ServiceProvider.
/// </summary>
internal static class TrainingCenterWindowFallbackDependencies
{
    public static IDialogService Dialogs { get; } = new DialogService();
    public static TrainingCenterStore Store { get; } = new();
    public static TrainingCenterImportService Import { get; } = new();
    public static IKnowledgeBaseDiagnosticsRunner KnowledgeBaseDiagnostics { get; }
        = new InfraKnowledgeBase.KnowledgeBaseDiagnosticsRunner();
    public static ReviewQueueService TrainingReviewQueue { get; } = ReviewQueueService.CreatePersistent();

    public static TrainingReviewSamSegmentationService CreateTrainingReviewSam()
    {
        var pipelineConfig = new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        return new TrainingReviewSamSegmentationService(
            new VisionPipelineTrainingReviewSamClient(pipelineConfig));
    }

    public static FewShotExampleStore CreateFewShotStore()
        => new();
}
