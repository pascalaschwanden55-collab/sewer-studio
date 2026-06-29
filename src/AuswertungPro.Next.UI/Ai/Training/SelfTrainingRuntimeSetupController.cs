using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class SelfTrainingRuntimeSetup : IDisposable
{
    public SelfTrainingRuntimeSetup(
        AiRuntimeSettings runtimeSettings,
        OllamaConfig retrievalConfig,
        TrainingCenterSettings trainingSettings,
        HttpClient kbHttpClient,
        SelfTrainingSession session)
    {
        RuntimeSettings = runtimeSettings ?? throw new ArgumentNullException(nameof(runtimeSettings));
        RetrievalConfig = retrievalConfig ?? throw new ArgumentNullException(nameof(retrievalConfig));
        TrainingSettings = trainingSettings ?? throw new ArgumentNullException(nameof(trainingSettings));
        KbHttpClient = kbHttpClient ?? throw new ArgumentNullException(nameof(kbHttpClient));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public AiRuntimeSettings RuntimeSettings { get; }

    public OllamaConfig RetrievalConfig { get; }

    public TrainingCenterSettings TrainingSettings { get; }

    public HttpClient KbHttpClient { get; }

    public SelfTrainingSession Session { get; }

    public void Dispose()
        => Session.Dispose();
}

public static class SelfTrainingRuntimeSetupController
{
    public static Task<SelfTrainingRuntimeSetup> PrepareAsync(
        Func<AiRuntimeSettings> loadRuntimeSettings,
        Func<Task<TrainingCenterSettings>> loadTrainingSettingsAsync,
        Func<OllamaConfig> loadRetrievalConfig,
        Func<OllamaConfig, HttpClient> getOrCreateKbHttpClient,
        AppSettings? appSettings,
        ICodeCatalogProvider? codeCatalog,
        Action<string> log)
        => PrepareAsync(
            loadRuntimeSettings,
            loadTrainingSettingsAsync,
            loadRetrievalConfig,
            getOrCreateKbHttpClient,
            (runtimeSettings, retrievalConfig, kbHttpClient, trainingSettings) =>
                SelfTrainingSessionController.Create(
                    runtimeSettings,
                    retrievalConfig,
                    kbHttpClient,
                    trainingSettings,
                    appSettings,
                    codeCatalog),
            log);

    public static async Task<SelfTrainingRuntimeSetup> PrepareAsync(
        Func<AiRuntimeSettings> loadRuntimeSettings,
        Func<Task<TrainingCenterSettings>> loadTrainingSettingsAsync,
        Func<OllamaConfig> loadRetrievalConfig,
        Func<OllamaConfig, HttpClient> getOrCreateKbHttpClient,
        Func<AiRuntimeSettings, OllamaConfig, HttpClient, TrainingCenterSettings, SelfTrainingSession> createSession,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(loadRuntimeSettings);
        ArgumentNullException.ThrowIfNull(loadTrainingSettingsAsync);
        ArgumentNullException.ThrowIfNull(loadRetrievalConfig);
        ArgumentNullException.ThrowIfNull(getOrCreateKbHttpClient);
        ArgumentNullException.ThrowIfNull(createSession);
        ArgumentNullException.ThrowIfNull(log);

        var runtimeSettings = loadRuntimeSettings();
        log(SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(
            runtimeSettings.OllamaBaseUri,
            runtimeSettings.VisionModel));

        var trainingSettings = await loadTrainingSettingsAsync().ConfigureAwait(false);
        var retrievalConfig = loadRetrievalConfig();
        var kbHttpClient = getOrCreateKbHttpClient(retrievalConfig);
        var session = createSession(
            runtimeSettings,
            retrievalConfig,
            kbHttpClient,
            trainingSettings);

        return new SelfTrainingRuntimeSetup(
            runtimeSettings,
            retrievalConfig,
            trainingSettings,
            kbHttpClient,
            session);
    }
}
