using System.Diagnostics;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

internal sealed record EvalFullChainExecutionResult(
    IReadOnlyList<EvalReviewedFullChainPrediction> Predictions,
    string SidecarVersion,
    SidecarDetectorQualification? ActualDetectorQualification,
    string VisionModel,
    string TextModel,
    int PipeDiameterMm);

/// <summary>
/// Fuehrt den menschlich geprueften Bildsatz durch den produktiven
/// DINO-SAM-Qwen-CodeMapping-QualityGate-Weg. YOLO-Detect und YOLO-cls sind fuer
/// diesen Lauf absichtlich gesperrt, damit das unqualifizierte Modell kein Beweis ist.
/// </summary>
internal static class EvalFullChainBenchmarkRunner
{
    public static async Task<EvalFullChainExecutionResult> RunAsync(
        IReadOnlyList<EvalReviewedDamageCase> cases,
        AiPlatformSettings settings,
        ICodeCatalogProvider catalog,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);

        var allowedCodes = catalog.AllowedCodes();
        if (allowedCodes.Count == 0)
            throw new InvalidDataException("Der aktive VSA-Katalog enthaelt keine Codes.");
        var pipeDiameterMm = settings.PipeDiameterMmOverride ?? 300;
        using var sidecarHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.SidecarTimeoutSec))
        };
        using var ollamaHttp = new HttpClient
        {
            Timeout = settings.OllamaRequestTimeout
        };
        using var rawVisionClient = new VisionPipelineClient(
            settings.SidecarUrl,
            sidecarHttp,
            settings.SidecarToken);
        var health = await RequireSidecarAsync(
                rawVisionClient,
                settings.SidecarUrl,
                ct)
            .ConfigureAwait(false);
        using var ollamaClient = new OllamaClient(
            settings.OllamaBaseUri,
            ollamaHttp,
            settings.OllamaRequestTimeout,
            settings.OllamaKeepAlive,
            settings.OllamaNumCtx);
        await RequireOllamaModelsAsync(ollamaClient, settings, ct).ConfigureAwait(false);
        var bypassClient = new EvalDetectorBypassVisionClient(rawVisionClient);
        var runtime = BuildRuntime(
            bypassClient,
            ollamaClient,
            ollamaHttp,
            settings,
            catalog,
            allowedCodes,
            pipeDiameterMm);
        var predictions = new List<EvalReviewedFullChainPrediction>(cases.Count);

        for (var index = 0; index < cases.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var reviewedCase = cases[index];
            var prediction = await RunFrameAsync(
                    reviewedCase.BenchmarkCase,
                    runtime,
                    ct)
                .ConfigureAwait(false);
            predictions.Add(prediction);
            progress?.Invoke(FormatProgress(index + 1, cases.Count, reviewedCase, prediction));
        }

        return new EvalFullChainExecutionResult(
            predictions,
            health.Version,
            health.DetectorQualification,
            settings.VisionModel,
            settings.TextModel,
            pipeDiameterMm);
    }

    private static EvalFullChainRuntime BuildRuntime(
        EvalDetectorBypassVisionClient visionClient,
        OllamaClient ollamaClient,
        HttpClient ollamaHttp,
        AiPlatformSettings settings,
        ICodeCatalogProvider catalog,
        IReadOnlyList<string> allowedCodes,
        int pipeDiameterMm)
        => new(
            visionClient,
            new EnhancedVisionAnalysisService(
                ollamaClient,
                settings.VisionModel,
                catalog),
            settings.ToPipelineConfig() with
            {
                MultiModelEnabled = true,
                Mode = PipelineMode.MultiModel,
                PipeDiameterMmOverride = pipeDiameterMm
            },
            settings.ToRuntimeSettings(),
            new RuleBasedAiSuggestionPlausibilityService(
                new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase)),
            ollamaHttp,
            allowedCodes);

    private static async Task<EvalReviewedFullChainPrediction> RunFrameAsync(
        EvalSetBenchmarkCase benchmarkCase,
        EvalFullChainRuntime runtime,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        runtime.VisionClient.ResetFrameCounters();
        try
        {
            return await ExecuteFrameAsync(
                    benchmarkCase,
                    runtime,
                    stopwatch,
                    ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return FailedPrediction(
                benchmarkCase.FrameFileName,
                stopwatch.ElapsedMilliseconds,
                runtime.VisionClient,
                ex.Message);
        }
    }

    private static async Task<EvalReviewedFullChainPrediction> ExecuteFrameAsync(
        EvalSetBenchmarkCase benchmarkCase,
        EvalFullChainRuntime runtime,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        var frameBytes = await File.ReadAllBytesAsync(
            benchmarkCase.ImagePath,
            ct).ConfigureAwait(false);
        var traceWriter = new CapturingPipelineTraceWriter();
        var multiModel = CreateMultiModel(runtime, traceWriter, frameBytes);
        var videoResult = await multiModel
            .AnalyzeAsync(benchmarkCase.ImagePath, ct: ct)
            .ConfigureAwait(false);
        var mappingResult = await MapDetectionsAsync(
                benchmarkCase,
                runtime,
                videoResult,
                ct)
            .ConfigureAwait(false);
        var selected = SelectPrediction(mappingResult);
        stopwatch.Stop();

        return BuildPrediction(
            benchmarkCase.FrameFileName,
            stopwatch.ElapsedMilliseconds,
            runtime.VisionClient,
            videoResult,
            mappingResult,
            selected,
            traceWriter.LastEntry);
    }

    private static MultiModelAnalysisService CreateMultiModel(
        EvalFullChainRuntime runtime,
        CapturingPipelineTraceWriter traceWriter,
        byte[] frameBytes)
    {
        var multiModel = new MultiModelAnalysisService(
            traceWriter,
            runtime.VisionClient,
            runtime.PipelineConfig,
            qwenVision: runtime.QwenVision,
            frameSource: (_, _, _, _, token) =>
                ReadSingleFrameAsync(frameBytes, token),
            durationProbe: (_, _) => Task.FromResult(1.0));

        // Auch das getrennte YOLO-cls darf weder filtern noch einen Code liefern.
        multiModel.UseClsPrefilter = false;
        multiModel.ClassifierDecisionEnabled = false;
        multiModel.ClassifierOnlyStructuralEnabled = false;
        multiModel.EstimatedReachLengthM = 1.0;
        return multiModel;
    }

    private static async Task<FullProtocolGenerationResult?> MapDetectionsAsync(
        EvalSetBenchmarkCase benchmarkCase,
        EvalFullChainRuntime runtime,
        VideoAnalysisResult videoResult,
        CancellationToken ct)
    {
        if (!videoResult.IsSuccess || videoResult.Detections.Count == 0)
            return null;

        using var generator = new FullProtocolGenerationService(
            runtime.RuntimeSettings,
            runtime.Plausibility,
            runtime.OllamaHttp,
            retrieval: DisabledEvalRetrievalService.Instance);
        return await generator.GenerateFromDetectionsAsync(
                videoResult.Detections,
                new FullProtocolGenerationRequest(
                    benchmarkCase.HoldingKey ?? benchmarkCase.Id,
                    benchmarkCase.ImagePath,
                    runtime.AllowedCodes),
                ct: ct)
            .ConfigureAwait(false);
    }

    private static MappedProtocolEntry? SelectPrediction(
        FullProtocolGenerationResult? mappingResult)
        => mappingResult?.MappedEntries
            .Where(item => !string.IsNullOrWhiteSpace(item.SuggestedCode))
            .OrderByDescending(item => item.Detection.SeverityLevel ?? 0)
            .ThenByDescending(item => item.QualityGateResult?.CompositeConfidence ?? 0)
            .ThenBy(item => item.SuggestedCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static EvalReviewedFullChainPrediction BuildPrediction(
        string frameFileName,
        long timeMs,
        EvalDetectorBypassVisionClient visionClient,
        VideoAnalysisResult videoResult,
        FullProtocolGenerationResult? mappingResult,
        MappedProtocolEntry? selected,
        PipelineTraceEntry? trace)
        => new(
            frameFileName,
            selected?.SuggestedCode
            ?? (videoResult.IsSuccess && videoResult.Detections.Count == 0 ? "LEER" : ""),
            selected?.Detection.SeverityLevel ?? 0,
            timeMs,
            BuildTechnicalError(videoResult, mappingResult, trace),
            DetectorBypassed: true,
            DinoCalled: visionClient.DinoCalls > 0,
            DinoBoxCount: visionClient.LastDinoResponse?.Detections.Count ?? 0,
            SamCalled: visionClient.SamCalls > 0,
            SamMaskCount: visionClient.LastSamResponse?.Masks.Count ?? 0,
            QwenVisionCalled: trace?.QwenCalled == true,
            QwenVisionFindingCount: trace?.QwenRawFindingCount ?? 0,
            CodeMappingCalled: mappingResult is not null,
            CodeMappingCount: mappingResult?.MappedEntries.Count ?? 0,
            QualityGate: selected?.QualityGateResult?.TrafficLight,
            QualityGateComposite: selected?.QualityGateResult?.CompositeConfidence,
            Degraded: videoResult.Degraded,
            Incomplete: videoResult.Incomplete,
            DropReason: trace?.DropReason);

    private static EvalReviewedFullChainPrediction FailedPrediction(
        string frameFileName,
        long timeMs,
        EvalDetectorBypassVisionClient visionClient,
        string error)
        => new(
            frameFileName,
            PredictedCode: "",
            Severity: 0,
            TimeMs: timeMs,
            Error: error,
            DetectorBypassed: true,
            DinoCalled: visionClient.DinoCalls > 0,
            DinoBoxCount: visionClient.LastDinoResponse?.Detections.Count ?? 0,
            SamCalled: visionClient.SamCalls > 0,
            SamMaskCount: visionClient.LastSamResponse?.Masks.Count ?? 0,
            QwenVisionCalled: false,
            QwenVisionFindingCount: 0,
            CodeMappingCalled: false,
            CodeMappingCount: 0,
            QualityGate: null,
            QualityGateComposite: null,
            Degraded: true,
            Incomplete: true,
            DropReason: "runtime_error");

    private static async Task<SidecarHealthResponse> RequireSidecarAsync(
        VisionPipelineClient client,
        Uri sidecarUrl,
        CancellationToken ct)
    {
        var result = await client.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
        if (!result.IsReachable)
            throw new InvalidOperationException(
                $"Sidecar nicht erreichbar: {sidecarUrl} ({result.Error})");
        if (!result.IsAuthorized)
            throw new InvalidOperationException(
                $"Sidecar lehnt das Zugriffstoken ab: {sidecarUrl}");
        var health = result.Health
                     ?? throw new InvalidOperationException("Sidecar-Health ist leer.");
        if (!health.HasRequiredModels)
        {
            throw new InvalidOperationException(
                "Sidecar meldet fehlende Pflichtmodelle: "
                + health.MissingRequiredModelsText);
        }

        return health;
    }

    private static async Task RequireOllamaModelsAsync(
        OllamaClient client,
        AiPlatformSettings settings,
        CancellationToken ct)
    {
        var installedModels = await client.ListModelNamesAsync(ct).ConfigureAwait(false);
        RequireModel(installedModels, settings.VisionModel, "Vision");
        RequireModel(installedModels, settings.TextModel, "Text");
    }

    private static async IAsyncEnumerable<FrameData> ReadSingleFrameAsync(
        byte[] frameBytes,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield return new FrameData(0, frameBytes);
    }

    private static string? BuildTechnicalError(
        VideoAnalysisResult videoResult,
        FullProtocolGenerationResult? mappingResult,
        PipelineTraceEntry? trace)
    {
        var primaryError = !videoResult.IsSuccess
            ? videoResult.Error
            : mappingResult is { IsSuccess: false }
                ? mappingResult.Error
                : null;
        return EvalReviewedFullChainScorer.DescribeTechnicalError(
            primaryError,
            videoResult.Incomplete,
            videoResult.DegradedReason,
            trace?.DropReason);
    }

    private static string FormatProgress(
        int current,
        int total,
        EvalReviewedDamageCase reviewedCase,
        EvalReviewedFullChainPrediction prediction)
    {
        var predicted = string.IsNullOrWhiteSpace(prediction.PredictedCode)
            ? "FEHLER"
            : prediction.PredictedCode;
        var gate = prediction.QualityGate?.ToString() ?? "-";
        var stages =
            $"DINO={prediction.DinoBoxCount}, SAM={prediction.SamMaskCount}, " +
            $"Qwen={(prediction.QwenVisionCalled ? "ja" : "nein")}, Gate={gate}";
        return
            $"[{current,2}/{total}] {reviewedCase.BenchmarkCase.FrameFileName}  " +
            $"GT={reviewedCase.BenchmarkCase.ExpectedFullCode}  PRED={predicted}  " +
            $"{stages}  {prediction.TimeMs} ms";
    }

    private static void RequireModel(
        IReadOnlyList<string> installedModels,
        string requiredModel,
        string role)
    {
        if (installedModels.Any(item =>
                string.Equals(item, requiredModel, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{role}-Modell '{requiredModel}' ist in Ollama nicht installiert.");
    }

    private sealed record EvalFullChainRuntime(
        EvalDetectorBypassVisionClient VisionClient,
        EnhancedVisionAnalysisService QwenVision,
        PipelineConfig PipelineConfig,
        AiRuntimeSettings RuntimeSettings,
        IAiSuggestionPlausibilityService Plausibility,
        HttpClient OllamaHttp,
        IReadOnlyList<string> AllowedCodes);
}

internal sealed class EvalDetectorBypassVisionClient(
    IVisionPipelineClient inner) : IVisionPipelineClient
{
    private const string BypassReason =
        "Eval-Prueflauf: YOLO-Detect bewusst ausgeschlossen";

    public int DinoCalls { get; private set; }
    public int SamCalls { get; private set; }
    public DinoResponse? LastDinoResponse { get; private set; }
    public SamResponse? LastSamResponse { get; private set; }

    public void ResetFrameCounters()
    {
        DinoCalls = 0;
        SamCalls = 0;
        LastDinoResponse = null;
        LastSamResponse = null;
    }

    public async Task<SidecarHealthResponse?> HealthCheckAsync(
        CancellationToken ct = default)
    {
        var health = await inner.HealthCheckAsync(ct).ConfigureAwait(false);
        return health is null ? null : ForceBypass(health);
    }

    public async Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(
        CancellationToken ct = default)
    {
        var result = await inner.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
        return result with
        {
            Health = result.Health is null ? null : ForceBypass(result.Health)
        };
    }

    public Task<YoloResponse> DetectYoloAsync(
        YoloRequest request,
        CancellationToken ct = default)
        => throw new InvalidOperationException(
            "YOLO-Detect darf im Full-Chain-Eval nicht aufgerufen werden.");

    public async Task<DinoResponse> DetectDinoAsync(
        DinoRequest request,
        CancellationToken ct = default)
    {
        DinoCalls++;
        LastDinoResponse = await inner.DetectDinoAsync(request, ct).ConfigureAwait(false);
        return LastDinoResponse;
    }

    public async Task<SamResponse> SegmentSamAsync(
        SamRequest request,
        CancellationToken ct = default)
    {
        SamCalls++;
        LastSamResponse = await inner.SegmentSamAsync(request, ct).ConfigureAwait(false);
        return LastSamResponse;
    }

    public Task<YoloClassifyResponse> ClassifyYoloAsync(
        YoloClassifyRequest request,
        CancellationToken ct = default)
        => throw new InvalidOperationException(
            "YOLO-cls darf im Full-Chain-Eval nicht aufgerufen werden.");

    private static SidecarHealthResponse ForceBypass(SidecarHealthResponse health)
        => health with
        {
            DetectorQualification = new SidecarDetectorQualification(
                false,
                BypassReason)
        };
}

internal sealed class CapturingPipelineTraceWriter : IPipelineTraceWriter
{
    public PipelineTraceEntry? LastEntry { get; private set; }

    public Task WriteAsync(PipelineTraceEntry entry)
    {
        LastEntry = entry;
        return Task.CompletedTask;
    }

    public Task WriteSummaryAsync(string runId, TelemetrySummary summary)
        => Task.CompletedTask;

    public string? ResolvePath(string runId) => null;

    public string? ResolveSummaryPath(string runId) => null;
}

internal sealed class DisabledEvalRetrievalService : IRetrievalService
{
    public static DisabledEvalRetrievalService Instance { get; } = new();

    public bool CheckModelConsistency() => true;
    public string? StoredEmbedModel => null;
    public bool HasModelMismatch => false;

    public Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        string queryText,
        int topK = 5,
        CancellationToken ct = default)
        => Task.FromException<IReadOnlyList<RetrievalResult>>(
            new InvalidOperationException(
                "KB-Kontext ist im isolierten Full-Chain-Eval deaktiviert."));
}
