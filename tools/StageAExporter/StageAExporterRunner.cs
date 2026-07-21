using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace SewerStudio.Tools.StageAExporter;

public sealed class StageAExporterRunner(TimeProvider timeProvider) : IStageAExporterRunner
{
    private readonly TimeProvider _timeProvider = timeProvider
        ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<TrainingYoloExportResult> RunAsync(
        StageAExporterCliOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var sampleStore = new TrainingSampleFileStore(options.SourceSamplesPath);
        sampleStore.ConfigureEvalProtection(options.EvalSetRoot);
        var catalog = new ManifestCodeCatalogProvider(options.CatalogPath);
        var classMap = new TrainingYoloClassMapFileStore(
            options.ClassMapPath,
            options.ClassMigrationPath,
            options.CatalogPath);
        var runtime = TrainingYoloExportRuntime.CreateLocal(
            new TrainingYoloExportRuntimeOptions(options.KnowledgeRoot, options.EvalSetRoot),
            sampleStore,
            catalog,
            classMap,
            _timeProvider);
        if (!runtime.DatasetRoot.Equals(options.DatasetRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der zentrale Datensatzpfad ist nicht eindeutig.");

        return await runtime.Coordinator.RunAsync(
                new TrainingYoloExportCommand(
                    _timeProvider.GetUtcNow(),
                    options.PlanOnly
                        ? TrainingYoloExportMode.PlanOnly
                        : TrainingYoloExportMode.Execute),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
