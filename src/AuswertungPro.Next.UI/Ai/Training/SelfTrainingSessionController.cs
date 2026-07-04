using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class SelfTrainingSession : IDisposable
{
    private readonly IReadOnlyList<IDisposable> _ownedResources;
    private bool _disposed;

    public SelfTrainingSession(
        string activeVisionModel,
        ISelfTrainingOrchestrator orchestrator,
        IReadOnlyList<IDisposable> ownedResources)
    {
        ActiveVisionModel = string.IsNullOrWhiteSpace(activeVisionModel)
            ? OllamaConfig.DefaultVisionModel
            : activeVisionModel;
        Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _ownedResources = ownedResources ?? throw new ArgumentNullException(nameof(ownedResources));
    }

    public string ActiveVisionModel { get; }

    public ISelfTrainingOrchestrator Orchestrator { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var resource in _ownedResources)
            resource.Dispose();
    }
}

public static class SelfTrainingSessionController
{
    public static string ResolveVisionModel(string? configuredModel)
        => string.IsNullOrWhiteSpace(configuredModel)
            ? OllamaConfig.DefaultVisionModel
            : configuredModel;

    public static SelfTrainingSession Create(
        AiRuntimeSettings runtimeSettings,
        OllamaConfig retrievalConfig,
        HttpClient kbHttpClient,
        TrainingCenterSettings trainingSettings,
        AppSettings? appSettings,
        ICodeCatalogProvider? codeCatalog)
    {
        ArgumentNullException.ThrowIfNull(runtimeSettings);
        ArgumentNullException.ThrowIfNull(retrievalConfig);
        ArgumentNullException.ThrowIfNull(kbHttpClient);
        ArgumentNullException.ThrowIfNull(trainingSettings);

        var visionModel = ResolveVisionModel(runtimeSettings.VisionModel);
        var ollamaClient = new OllamaClient(
            runtimeSettings.OllamaBaseUri,
            ownedTimeout: runtimeSettings.OllamaRequestTimeout,
            keepAlive: runtimeSettings.OllamaKeepAlive,
            numCtx: runtimeSettings.OllamaNumCtx);
        var vision = new EnhancedVisionAnalysisService(ollamaClient, visionModel, codeCatalog);
        var comparison = new SelfTrainingComparisonService();
        var technique = new TechniqueAssessmentService(ollamaClient, visionModel);
        var pdfExtractor = new PdfProtocolExtractor();

        var kbContext = new KnowledgeBaseContext();
        var retrieval = new RetrievalService(kbContext, new EmbeddingService(kbHttpClient, retrievalConfig));
        var evalHaltungen = EvalContaminationSetProvider.Load(appSettings).HaltungKeys;

        var orchestrator = new SelfTrainingOrchestrator(
            vision,
            comparison,
            technique,
            pdfExtractor,
            trainingSettings,
            TrainingFfmpegPathResolver.Resolve(runtimeSettings.FfmpegPath),
            retrieval,
            evalHaltungen);

        return new SelfTrainingSession(
            visionModel,
            orchestrator,
            new IDisposable[] { kbContext, ollamaClient });
    }
}
