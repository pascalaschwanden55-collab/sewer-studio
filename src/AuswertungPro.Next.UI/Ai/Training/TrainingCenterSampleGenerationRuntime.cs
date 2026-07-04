using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterSampleGenerationRuntime
{
    public static async Task<TrainingSampleGenerationResult> GenerateWithDiagnosticsAsync(
        ICodeCatalogProvider? codeCatalog,
        TrainingCaseInput input,
        IReadOnlyCollection<string> existingSignatures,
        CancellationToken ct)
    {
        var cfg = new AppSettingsAiSettingsProvider()
            .Load()
            .ToRuntimeSettings();
        var settings = await TrainingCenterSettingsStore.LoadAsync().ConfigureAwait(false);
        var meterSvc = TrainingMeterTimelineServiceFactory.Create(cfg, settings.GpuConcurrency);
        var generator = new TrainingSampleGenerator(cfg, meterSvc, settings, codeCatalog);

        return await generator.GenerateWithDiagnosticsAsync(
            input,
            existingSignatures,
            framesDir: null,
            ct).ConfigureAwait(false);
    }
}
