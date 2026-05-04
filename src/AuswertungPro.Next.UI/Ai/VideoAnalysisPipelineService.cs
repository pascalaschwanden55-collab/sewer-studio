using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Haupt-Einstiegspunkt für den kombinierten Videoanalyse-Workflow.
///
/// BUG 1.3 FIX: Video wird nur EINMAL analysiert.
/// Ablauf:
///   1) VideoFullAnalysisService.AnalyzeAsync()  → RawVideoDetections
///   2) FullProtocolGenerationService.GenerateFromDetectionsAsync()  → ProtocolDocument
///      (kein eigenes AnalyzeAsync mehr!)
/// </summary>
public sealed class VideoAnalysisPipelineService : IVideoAnalysisPipelineService
{
    private readonly AiRuntimeConfig _cfg;
    private readonly IAiSuggestionPlausibilityService _plausibility;
    private readonly HttpClient _httpClient;

    public VideoAnalysisPipelineService(
        AiRuntimeConfig cfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient)
    {
        _cfg = cfg;
        _plausibility = plausibility;
        _httpClient = httpClient;
    }

    public async Task<PipelineResult> RunAsync(
        PipelineRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled)
            return PipelineResult.Failed("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).");

        // ── Decide: Multi-Model or Ollama-Only ────────────────────────────────
        var (useMultiModel, pipelineCfg) = await ShouldUseMultiModelAsync(ct).ConfigureAwait(false);

        // ── Qwen vorladen damit der erste Frame nicht haengt ──────────────────
        try
        {
            progress?.Report(new PipelineProgress(
                PipelinePhase.VideoAnalysis, 0, "Qwen Vision-Modell wird geladen...",
                FramesDone: 0, FramesTotal: 0));

            var preloadClient = _cfg.CreateOllamaClient(null);
            await preloadClient.EnsureModelLoadedAsync(_cfg.VisionModel, 0, ct: ct).ConfigureAwait(false);
            preloadClient.Dispose();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Pipeline] Modell-Vorladen fehlgeschlagen: {ex.Message}");
        }

        // ── Phase 1: Video-Analyse ────────────────────────────────────────────
        var modeLabel = useMultiModel ? "MULTI-MODEL (YOLO+DINO+SAM+Qwen)" : "OLLAMA-ONLY (nur Qwen)";
        System.Diagnostics.Debug.WriteLine($"[Pipeline] Modus: {modeLabel} fuer {request.HaltungId}");
        progress?.Report(new PipelineProgress(
            useMultiModel ? PipelinePhase.MultiModelDetection : PipelinePhase.VideoAnalysis,
            0, $"{modeLabel}",
            FramesDone: 0, FramesTotal: 0));

        var analysisProgress = new Progress<VideoAnalysisProgress>(p =>
            progress?.Report(new PipelineProgress(
                useMultiModel ? PipelinePhase.MultiModelDetection : PipelinePhase.VideoAnalysis,
                p.Percent, p.Status,
                FramesDone: p.FramesDone,
                FramesTotal: p.FramesTotal,
                FramePreviewPng: p.FramePreviewPng,
                LiveFindings: p.LiveFindings)));

        VideoAnalysisResult videoResult;

        if (useMultiModel)
        {
            // ── Multi-Model Path: YOLO -> DINO -> SAM -> Qwen ──
            // Eigener HttpClient fuer den Sidecar (nicht den geteilten _httpClient verwenden,
            // weil BaseAddress nur einmal gesetzt werden kann und _httpClient evtl.
            // bereits fuer Ollama konfiguriert ist)
            using var sidecarHttp = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(pipelineCfg.SidecarTimeoutSec)
            };
            var pipelineClient = new VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);

            // Create Qwen vision service for VSA-Code enrichment
            var ollamaClient = _cfg.CreateOllamaClient(_httpClient);
            // Phase 0.4: Video-Pipeline nutzt vollen Damage-Prompt (mit Aufnahmetechnik).
            var qwenVision = new EnhancedVisionAnalysisService(ollamaClient, _cfg.VisionModel, _cfg.ReferenceVisionModel, useFullDamagePrompt: true);
            await EnableFewShotIfAvailableAsync(qwenVision, ct).ConfigureAwait(false);

            var multiModel = new MultiModelAnalysisService(
                pipelineClient, pipelineCfg,
                _cfg.FfmpegPath ?? "ffmpeg",
                qwenVision: qwenVision);
            multiModel.FrameStepSeconds = request.FrameStepSeconds;
            multiModel.DedupWindowFrames = request.DedupWindowFrames;

            videoResult = await multiModel.AnalyzeAsync(
                request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
        }
        else
        {
            // ── Ollama-Only Path (existing behavior) ──
            var client = _cfg.CreateOllamaClient(_httpClient);
            // Phase 0.4: Video-Pipeline nutzt vollen Damage-Prompt (mit Aufnahmetechnik).
            var ollamaVision = new EnhancedVisionAnalysisService(client, _cfg.VisionModel, _cfg.ReferenceVisionModel, useFullDamagePrompt: true);
            await EnableFewShotIfAvailableAsync(ollamaVision, ct).ConfigureAwait(false);
            var videoService = new VideoFullAnalysisService(
                vision: ollamaVision,
                ffmpegPath: _cfg.FfmpegPath ?? "ffmpeg");

            videoService.FrameStepSeconds = request.FrameStepSeconds;
            videoService.DedupWindowFrames = request.DedupWindowFrames;

            // Knick-Erkennung (BAG) einhaengen NUR wenn Sidecar wirklich aktiv ist.
            // Wichtig: Bisheriger Code pruefte `sidecarCfg is not null`, aber PipelineConfig.Load()
            // gibt nie null zurueck -> Knick-HTTP lief auch im OllamaOnly-Modus mit Timeouts pro Frame.
            // Jetzt: nur wenn ShouldUseMultiModel wirklich Sidecar-Modus signalisiert.
            // knickHttp darf NICHT via `using` im if-Scope leben — Dispose erst nach AnalyzeAsync.
            System.Net.Http.HttpClient? knickHttp = null;
            try
            {
                var (sidecarActive, sidecarCfg) = await ShouldUseMultiModelAsync(ct).ConfigureAwait(false);
                if (sidecarActive && sidecarCfg is not null)
                {
                    knickHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var knickClient = new VisionPipelineClient(sidecarCfg.SidecarUrl, knickHttp);
                    videoService.KnickDetection = new KnickDetectionService(knickClient);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[Pipeline] Knick-Erkennung deaktiviert (Sidecar nicht aktiv im aktuellen Modus).");
                }
            }
            catch { /* Knick-Erkennung optional */ }

            try
            {
                videoResult = await videoService.AnalyzeAsync(
                    request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
            }
            finally
            {
                knickHttp?.Dispose();
            }
        }

        var resultLabel = videoResult.IsSuccess
            ? $"OK: {videoResult.Detections?.Count ?? 0} Detektionen in {videoResult.FramesAnalyzed} Frames"
            : $"FEHLER: {videoResult.Error}";
        System.Diagnostics.Debug.WriteLine($"[Pipeline] Ergebnis: {resultLabel}");
        progress?.Report(new PipelineProgress(
            PipelinePhase.VideoAnalysis, 100, resultLabel,
            FramesDone: videoResult.FramesAnalyzed, FramesTotal: videoResult.FramesAnalyzed));

        if (!videoResult.IsSuccess)
            return PipelineResult.Failed($"Video-Analyse fehlgeschlagen: {videoResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
            $"{videoResult.Detections?.Count ?? 0} Schäden erkannt in {videoResult.FramesAnalyzed} Frames.",
            FramesDone: videoResult.FramesAnalyzed,
            FramesTotal: videoResult.FramesAnalyzed));

        // ── Phase 2: Code-Mapping (mit bereits analysierten Detections) ───────
        // BUG 1.3 FIX: GenerateFromDetectionsAsync statt GenerateAsync
        // → kein zweites AnalyzeAsync mehr!
        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 0,
            "Starte Code-Mapping..."));

        using var generator = new FullProtocolGenerationService(_cfg, _plausibility, _httpClient);

        var mappingProgress = new Progress<CodeMappingProgress>(p =>
            progress?.Report(new PipelineProgress(
                PipelinePhase.CodeMapping, p.Percent, p.Status,
                ItemsDone: p.Done,
                ItemsTotal: p.Total)));

        var genRequest = new FullProtocolGenerationRequest(
            HaltungId: request.HaltungId,
            VideoPath: request.VideoPath,
            AllowedCodes: request.AllowedCodes,
            ProjectFolderAbs: request.ProjectFolderAbs,
            RequestedBy: request.RequestedBy);

        // BUG 1.3 FIX: Detections direkt übergeben
        var genResult = await generator.GenerateFromDetectionsAsync(
            videoResult.Detections ?? [], genRequest, mappingProgress, ct).ConfigureAwait(false);

        if (!genResult.IsSuccess)
            return PipelineResult.Failed($"Code-Mapping fehlgeschlagen: {genResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 100,
            $"{genResult.MappedEntries.Count(e => e.SuggestedCode != null)} Einträge gemappt.",
            ItemsDone: genResult.MappedEntries.Count,
            ItemsTotal: genResult.MappedEntries.Count));

        progress?.Report(new PipelineProgress(PipelinePhase.Done, 100, "Fertig."));

        return new PipelineResult(
            Document: genResult.Document,
            Detections: videoResult.Detections ?? [],
            MappedEntries: genResult.MappedEntries,
            Stats: new PipelineStats(
                FramesAnalyzed: videoResult.FramesAnalyzed,
                DurationSeconds: videoResult.DurationSeconds,
                DetectionsRaw: videoResult.Detections?.Count ?? 0,
                EntriesGenerated: genResult.Document?.Current?.Entries?.Count ?? 0,
                EntriesWithHighConfidence: genResult.MappedEntries.Count(e => e.Confidence >= 0.75)),
            Warnings: genResult.Warnings,
            Error: null,
            Telemetry: videoResult.Telemetry);
    }

    private static async Task EnableFewShotIfAvailableAsync(
        EnhancedVisionAnalysisService vision,
        CancellationToken ct)
    {
        try
        {
            var fewShotStore = new FewShotExampleStore();
            await vision.EnableFewShotAsync(fewShotStore, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Pipeline] Few-Shot Aktivierung fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines whether the Multi-Model pipeline should be used.
    /// - OllamaOnly: never use sidecar.
    /// - Auto: check sidecar health, use if available, fall back to Ollama otherwise.
    /// - MultiModel: require sidecar (error if not reachable).
    /// </summary>
    private async Task<(bool UseMultiModel, PipelineConfig Config)> ShouldUseMultiModelAsync(CancellationToken ct)
    {
        var pipelineCfg = PipelineConfig.Load();

        if (pipelineCfg.Mode == PipelineMode.OllamaOnly)
        {
            System.Diagnostics.Debug.WriteLine("[Pipeline] Mode=OllamaOnly → kein Sidecar");
            return (false, pipelineCfg);
        }

        if (!pipelineCfg.MultiModelEnabled && pipelineCfg.Mode != PipelineMode.MultiModel)
        {
            System.Diagnostics.Debug.WriteLine("[Pipeline] MultiModelEnabled=false → kein Sidecar");
            return (false, pipelineCfg);
        }

        // Check sidecar health
        try
        {
            using var healthHttp = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, healthHttp);
            var health = await client.HealthCheckAsync(ct).ConfigureAwait(false);

            if (health is null || health.Status != "ok")
            {
                System.Diagnostics.Debug.WriteLine($"[Pipeline] Sidecar Health: {health?.Status ?? "null"} → Ollama-Only");
                if (pipelineCfg.Mode == PipelineMode.MultiModel)
                    throw new InvalidOperationException(
                        $"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}), aber PipelineMode=MultiModel erzwungen.");
                return (false, pipelineCfg);
            }

            System.Diagnostics.Debug.WriteLine("[Pipeline] Sidecar Health: ok → MULTI-MODEL aktiv");
            return (true, pipelineCfg);
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Pipeline] Sidecar Health-Check EXCEPTION: {ex.GetType().Name}: {ex.Message} → Ollama-Only");
            if (pipelineCfg.Mode == PipelineMode.MultiModel)
                throw;
            return (false, pipelineCfg);
        }
    }
}

// ── Request / Result ──────────────────────────────────────────────────────────

public sealed record PipelineRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null,
    double FrameStepSeconds = 3.0,
    int DedupWindowFrames = 3
);

public sealed record PipelineResult(
    ProtocolDocument? Document,
    IReadOnlyList<RawVideoDetection> Detections,
    IReadOnlyList<MappedProtocolEntry> MappedEntries,
    PipelineStats? Stats,
    IReadOnlyList<string> Warnings,
    string? Error,
    Pipeline.TelemetrySummary? Telemetry = null)
{
    public bool IsSuccess => Error is null;

    public static PipelineResult Failed(string error) =>
        new(null, Array.Empty<RawVideoDetection>(),
            Array.Empty<MappedProtocolEntry>(), null,
            Array.Empty<string>(), error);
}

public sealed record PipelineStats(
    int FramesAnalyzed,
    double DurationSeconds,
    int DetectionsRaw,
    int EntriesGenerated,
    int EntriesWithHighConfidence
);

public sealed record PipelineProgress(
    PipelinePhase Phase,
    double PercentInPhase,
    string Status,
    int? FramesDone = null,
    int? FramesTotal = null,
    int? ItemsDone = null,
    int? ItemsTotal = null,
    byte[]? FramePreviewPng = null,
    IReadOnlyList<LiveFrameFinding>? LiveFindings = null);

public enum PipelinePhase
{
    VideoAnalysis,
    MultiModelDetection,
    CodeMapping,
    Done
}
