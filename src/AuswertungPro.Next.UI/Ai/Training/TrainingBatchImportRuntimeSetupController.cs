using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportRuntimeSetupResult<TGenerator>(
    AiRuntimeSettings Config,
    TrainingCenterSettings Settings,
    TGenerator Generator,
    List<TrainingSample> AllSamples,
    HashSet<string> ExistingSignatures,
    IReadOnlyList<TrainingCase> CasesToProcess,
    TrainingBatchImportRunSummary RunSummary);

public static class TrainingBatchImportRuntimeSetupController
{
    public static async Task<TrainingBatchImportRuntimeSetupResult<TGenerator>> PrepareAsync<TGenerator>(
        IReadOnlyList<TrainingCase> casesWithProtocol,
        Func<AiRuntimeSettings> loadConfig,
        Func<Task<TrainingCenterSettings>> loadSettingsAsync,
        Func<AiRuntimeSettings, TrainingCenterSettings, TGenerator> createGenerator,
        Func<Task<List<TrainingSample>>> loadSamplesAsync,
        Action<int> setProgressMax,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(casesWithProtocol);
        ArgumentNullException.ThrowIfNull(loadConfig);
        ArgumentNullException.ThrowIfNull(loadSettingsAsync);
        ArgumentNullException.ThrowIfNull(createGenerator);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(setProgressMax);
        ArgumentNullException.ThrowIfNull(log);

        var cfg = loadConfig();
        log($"AI Config: Enabled={cfg.Enabled}, ffmpeg={cfg.FfmpegPath}");

        var settings = await loadSettingsAsync().ConfigureAwait(false);
        var generator = createGenerator(cfg, settings);

        var sampleSnapshot = await TrainingBatchImportExistingSampleSnapshotController.LoadAsync(
            loadSamplesAsync,
            log).ConfigureAwait(false);

        setProgressMax(casesWithProtocol.Count);
        return new TrainingBatchImportRuntimeSetupResult<TGenerator>(
            cfg,
            settings,
            generator,
            sampleSnapshot.AllSamples,
            sampleSnapshot.ExistingSignatures,
            casesWithProtocol,
            new TrainingBatchImportRunSummary());
    }
}
