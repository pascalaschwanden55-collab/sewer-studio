using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Pipeline;

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

        // ── Phase 1: Video-Analyse ────────────────────────────────────────────
        progress?.Report(new PipelineProgress(
            useMultiModel ? PipelinePhase.MultiModelDetection : PipelinePhase.VideoAnalysis,
            0, useMultiModel ? "Starte Multi-Model Pipeline..." : "Starte Video-Analyse...",
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
            var pipelineClient = new VisionPipelineClient(pipelineCfg.SidecarUrl, _httpClient);

            // Create Qwen vision service for VSA-Code enrichment
            var ollamaClient = _cfg.CreateOllamaClient(_httpClient);
            var qwenVision = new EnhancedVisionAnalysisService(ollamaClient, _cfg.VisionModel);

            var multiModel = new MultiModelAnalysisService(
                pipelineClient, pipelineCfg,
                _cfg.FfmpegPath ?? "ffmpeg",
                qwenVision: qwenVision);
            multiModel.FrameStepSeconds = request.FrameStepSeconds;
            multiModel.DedupWindowFrames = request.DedupWindowFrames;
            multiModel.HaltungslaengeM = request.HaltungslaengeM;

            videoResult = await multiModel.AnalyzeAsync(
                request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
        }
        else
        {
            // ── Ollama-Only Path (existing behavior) ──
            var client = _cfg.CreateOllamaClient(_httpClient);
            var videoService = VideoFullAnalysisService.Create(
                client: client,
                visionModel: _cfg.VisionModel,
                ffmpegPath: _cfg.FfmpegPath ?? "ffmpeg");

            videoService.FrameStepSeconds = request.FrameStepSeconds;
            videoService.DedupWindowFrames = request.DedupWindowFrames;

            videoResult = await videoService.AnalyzeAsync(
                request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
        }

        if (!videoResult.IsSuccess)
            return PipelineResult.Failed($"Video-Analyse fehlgeschlagen: {videoResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
            $"{videoResult.Detections.Count} Schäden erkannt in {videoResult.FramesAnalyzed} Frames.",
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
            RequestedBy: request.RequestedBy,
            HaltungslaengeM: request.HaltungslaengeM);

        // BUG 1.3 FIX: Detections direkt übergeben
        var genResult = await generator.GenerateFromDetectionsAsync(
            videoResult.Detections, genRequest, mappingProgress, ct).ConfigureAwait(false);

        if (!genResult.IsSuccess)
            return PipelineResult.Failed($"Code-Mapping fehlgeschlagen: {genResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 100,
            $"{genResult.MappedEntries.Count(e => e.SuggestedCode != null)} Einträge gemappt.",
            ItemsDone: genResult.MappedEntries.Count,
            ItemsTotal: genResult.MappedEntries.Count));

        progress?.Report(new PipelineProgress(PipelinePhase.Done, 100, "Fertig."));

        return new PipelineResult(
            Document: genResult.Document,
            Detections: videoResult.Detections,
            MappedEntries: genResult.MappedEntries,
            Stats: new PipelineStats(
                FramesAnalyzed: videoResult.FramesAnalyzed,
                DurationSeconds: videoResult.DurationSeconds,
                DetectionsRaw: videoResult.Detections.Count,
                EntriesGenerated: genResult.Document?.Current?.Entries?.Count ?? 0,
                EntriesWithHighConfidence: genResult.MappedEntries.Count(e => e.Confidence >= 0.75)),
            Warnings: genResult.Warnings,
            Error: null,
            Telemetry: videoResult.Telemetry);
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
            return (false, pipelineCfg);

        // MultiModelEnabled ist ein Master-Kill-Switch.
        // Nur ein explizites Mode=MultiModel übersteuert ihn.
        if (!pipelineCfg.MultiModelEnabled && pipelineCfg.Mode != PipelineMode.MultiModel)
            return (false, pipelineCfg);

        // Check sidecar health
        try
        {
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, _httpClient);
            var health = await client.HealthCheckAsync(ct).ConfigureAwait(false);

            if (health is null || health.Status != "ok")
            {
                if (pipelineCfg.Mode == PipelineMode.MultiModel)
                    throw new InvalidOperationException(
                        $"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}), aber PipelineMode=MultiModel erzwungen.");
                return (false, pipelineCfg);
            }

            return (true, pipelineCfg);
        }
        catch (InvalidOperationException) { throw; }
        catch
        {
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
    int DedupWindowFrames = 3,
    double HaltungslaengeM = 0
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
