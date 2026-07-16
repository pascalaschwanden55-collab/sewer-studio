using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Alle Dienste, die das Training-Center-Fenster zum Start benoetigt.
/// Der Fenster-Code erhaelt damit ein fertiges Paket und baut keine Speicher selbst zusammen.
/// </summary>
internal sealed record TrainingCenterWindowDependencies(
    IDialogService Dialogs,
    TrainingCenterStore Store,
    TrainingCenterImportService Import,
    IKnowledgeBaseDiagnosticsRunner KnowledgeBaseDiagnostics,
    Func<ReviewQueueService> CreateReviewQueue,
    Func<TrainingReviewSamSegmentationService> CreateReviewSam,
    Func<FewShotExampleStore> CreateFewShotStore);

/// <summary>
/// Zentraler Aufbau fuer den normalen Programmweg und den parameterlosen WPF-/Designer-Rueckfall.
/// </summary>
internal static class TrainingCenterWindowDependencyFactory
{
    private static IDialogService DefaultDialogs { get; } = new DialogService();
    private static TrainingCenterStore DefaultStore { get; } = new();
    private static TrainingCenterImportService DefaultImport { get; } = new();
    private static IKnowledgeBaseDiagnosticsRunner DefaultKnowledgeBaseDiagnostics { get; }
        = new InfraKnowledgeBase.KnowledgeBaseDiagnosticsRunner();
    private static ReviewQueueService DefaultTrainingReviewQueue { get; }
        = ReviewQueueService.CreatePersistent();

    internal static TrainingCenterWindowDependencies Create(ServiceProvider? services)
    {
        if (services is null)
        {
            return Create(
                services: null,
                DefaultDialogs,
                DefaultStore,
                DefaultImport,
                DefaultKnowledgeBaseDiagnostics);
        }

        return Create(
            services,
            services.Dialogs,
            services.TrainingCenterStore,
            services.TrainingCenterImport,
            services.KnowledgeBaseDiagnostics);
    }

    internal static TrainingCenterWindowDependencies Create(
        ServiceProvider? services,
        IDialogService dialogs,
        TrainingCenterStore store,
        TrainingCenterImportService import,
        IKnowledgeBaseDiagnosticsRunner knowledgeBaseDiagnostics)
        => new(
            dialogs,
            store,
            import,
            knowledgeBaseDiagnostics,
            () => services?.TrainingReviewQueue ?? DefaultTrainingReviewQueue,
            () => services?.CreateTrainingReviewSam() ?? CreateDefaultTrainingReviewSam(),
            () => services?.CreateFewShotStore() ?? new FewShotExampleStore());

    private static TrainingReviewSamSegmentationService CreateDefaultTrainingReviewSam()
    {
        var pipelineConfig = new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();
        return new TrainingReviewSamSegmentationService(
            new VisionPipelineTrainingReviewSamClient(pipelineConfig));
    }
}
