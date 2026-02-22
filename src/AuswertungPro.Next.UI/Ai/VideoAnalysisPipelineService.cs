using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Haupt-Einstiegspunkt für den Pallon-ähnlichen Workflow.
///
/// BUG 1.3 FIX: Video wird nur EINMAL analysiert.
/// Ablauf:
///   1) VideoFullAnalysisService.AnalyzeAsync()  → RawVideoDetections
///   2) FullProtocolGenerationService.GenerateFromDetectionsAsync()  → ProtocolDocument
///      (kein eigenes AnalyzeAsync mehr!)
/// </summary>
public sealed class VideoAnalysisPipelineService
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
            return PipelineResult.Failed("KI ist deaktiviert (AUSWERTUNGPRO_AI_ENABLED=0).");

        // Einen gemeinsamen OllamaClient für die gesamte Pipeline
        var client = new OllamaClient(_cfg.OllamaBaseUri, _httpClient);

        // ── Phase 1: Video-Analyse (einmalig) ────────────────────────────────
        progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 0,
            "Starte Video-Analyse..."));

        // BUG 1.3 FIX: VideoFullAnalysisService direkt mit EnhancedVision erstellen
        var videoService = VideoFullAnalysisService.Create(
            client: client,
            visionModel: _cfg.VisionModel,
            ffmpegPath: _cfg.FfmpegPath ?? "ffmpeg");

        videoService.FrameStepSeconds = request.FrameStepSeconds;
        videoService.DedupWindowFrames = request.DedupWindowFrames;

        var analysisProgress = new Progress<VideoAnalysisProgress>(p =>
            progress?.Report(new PipelineProgress(
                PipelinePhase.VideoAnalysis, p.Percent, p.Status)));

        var videoResult = await videoService.AnalyzeAsync(
            request.VideoPath, analysisProgress, ct).ConfigureAwait(false);

        if (!videoResult.IsSuccess)
            return PipelineResult.Failed($"Video-Analyse fehlgeschlagen: {videoResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
            $"{videoResult.Detections.Count} Schäden erkannt in {videoResult.FramesAnalyzed} Frames."));

        // ── Phase 2: Code-Mapping (mit bereits analysierten Detections) ───────
        // BUG 1.3 FIX: GenerateFromDetectionsAsync statt GenerateAsync
        // → kein zweites AnalyzeAsync mehr!
        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 0,
            "Starte Code-Mapping..."));

        var generator = new FullProtocolGenerationService(_cfg, _plausibility, _httpClient);

        var mappingProgress = new Progress<CodeMappingProgress>(p =>
            progress?.Report(new PipelineProgress(
                PipelinePhase.CodeMapping, p.Percent, p.Status)));

        var genRequest = new FullProtocolGenerationRequest(
            HaltungId: request.HaltungId,
            VideoPath: request.VideoPath,
            AllowedCodes: request.AllowedCodes,
            ProjectFolderAbs: request.ProjectFolderAbs,
            RequestedBy: request.RequestedBy);

        // BUG 1.3 FIX: Detections direkt übergeben
        var genResult = await generator.GenerateFromDetectionsAsync(
            videoResult.Detections, genRequest, mappingProgress, ct).ConfigureAwait(false);

        if (!genResult.IsSuccess)
            return PipelineResult.Failed($"Code-Mapping fehlgeschlagen: {genResult.Error}");

        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 100,
            $"{genResult.MappedEntries.Count(e => e.SuggestedCode != null)} Einträge gemappt."));

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
            Error: null);
    }
}

// ── Request / Result ──────────────────────────────────────────────────────────

public sealed record PipelineRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null,
    double FrameStepSeconds = 2.0,
    int DedupWindowFrames = 3
);

public sealed record PipelineResult(
    ProtocolDocument? Document,
    IReadOnlyList<RawVideoDetection> Detections,
    IReadOnlyList<MappedProtocolEntry> MappedEntries,
    PipelineStats? Stats,
    IReadOnlyList<string> Warnings,
    string? Error)
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
    string Status);

public enum PipelinePhase
{
    VideoAnalysis,
    CodeMapping,
    Done
}
