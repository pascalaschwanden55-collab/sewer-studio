using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Haupt-Einstiegspunkt fÃ¼r den kombinierten Videoanalyse-Workflow.
///
/// BUG 1.3 FIX: Video wird nur EINMAL analysiert.
/// Ablauf:
///   1) VideoFullAnalysisService.AnalyzeAsync()  â†’ RawVideoDetections
///   2) FullProtocolGenerationService.GenerateFromDetectionsAsync()  â†’ ProtocolDocument
///      (kein eigenes AnalyzeAsync mehr!)
/// </summary>
public sealed class VideoAnalysisPipelineService : IVideoAnalysisPipelineService
{
    private readonly AiRuntimeSettings _cfg;
    private readonly PipelineConfig _pipelineCfg;
    private readonly IAiSuggestionPlausibilityService _plausibility;
    private readonly HttpClient _httpClient;
    private readonly ICodeCatalogProvider? _codeCatalog;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IPipelineTraceWriter _pipelineTraceWriter;
    private readonly IProcessOutputReader _processOutputs;
    private readonly IPipelineEnvironmentOptions _pipelineEnvironmentOptions;
    private readonly ISidecarTelemetryWriter _sidecarTelemetry;
    // Kontrollierter Sidecar-Neustart (Paket 3/A2): null = kein Neustart (heutiges Verhalten).
    private readonly Application.Ai.Startup.ISidecarRestartService? _sidecarRestart;

    public VideoAnalysisPipelineService(
        AiRuntimeSettings cfg,
        PipelineConfig pipelineCfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null,
        Application.Ai.Startup.ISidecarRestartService? sidecarRestart = null)
        : this(
            PipelineTraceWriter.Current,
            ProcessOutputReader.Current,
            cfg,
            pipelineCfg,
            plausibility,
            httpClient,
            codeCatalog,
            loggerFactory,
            pipelineEnvironmentOptions,
            sidecarTelemetry,
            sidecarRestart)
    {
    }

    public VideoAnalysisPipelineService(
        IPipelineTraceWriter pipelineTraceWriter,
        AiRuntimeSettings cfg,
        PipelineConfig pipelineCfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null,
        Application.Ai.Startup.ISidecarRestartService? sidecarRestart = null)
        : this(
            pipelineTraceWriter,
            ProcessOutputReader.Current,
            cfg,
            pipelineCfg,
            plausibility,
            httpClient,
            codeCatalog,
            loggerFactory,
            pipelineEnvironmentOptions,
            sidecarTelemetry,
            sidecarRestart)
    {
    }

    public VideoAnalysisPipelineService(
        IPipelineTraceWriter pipelineTraceWriter,
        IProcessOutputReader processOutputs,
        AiRuntimeSettings cfg,
        PipelineConfig pipelineCfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null,
        Application.Ai.Startup.ISidecarRestartService? sidecarRestart = null)
    {
        _pipelineTraceWriter = pipelineTraceWriter ?? throw new ArgumentNullException(nameof(pipelineTraceWriter));
        _processOutputs = processOutputs ?? throw new ArgumentNullException(nameof(processOutputs));
        _pipelineEnvironmentOptions = pipelineEnvironmentOptions ?? PipelineEnvironmentOptions.Current;
        _sidecarTelemetry = sidecarTelemetry ?? SidecarTelemetryWriter.Current;
        _sidecarRestart = sidecarRestart;
        _cfg = cfg;
        _pipelineCfg = pipelineCfg;
        _plausibility = plausibility;
        _httpClient = httpClient;
        _codeCatalog = codeCatalog;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<VideoAnalysisPipelineService>();
    }

    public async Task<PipelineResult> RunAsync(
        PipelineRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled)
            return PipelineResult.Failed("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).");

        // â”€â”€ Decide: Multi-Model or Ollama-Only â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var (useMultiModel, pipelineCfg, fallbackReason) = await ShouldUseMultiModelAsync(ct).ConfigureAwait(false);

        // Unerwarteten Fallback klar sichtbar machen (sonst sieht Ollama-Only wie Normalbetrieb aus).
        if (fallbackReason is not null)
        {
            _logger.LogWarning("Videoanalyse-Pipeline Warnung: {Reason}", fallbackReason);
            progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 0,
                "WARNUNG: " + fallbackReason, FramesDone: 0, FramesTotal: 0));
        }

        _logger.LogInformation(
            "Videoanalyse-Pipeline gestartet: Modus={Mode}, Haltung={Haltung}, Video={Video}",
            useMultiModel ? "Multi-Model" : "Ollama-Only", request.HaltungId, Path.GetFileName(request.VideoPath));

        // â”€â”€ Phase 1: Video-Analyse â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // U3: Ein ffmpeg-Haenger bei der Frame-Extraktion wirft jetzt VideoFrameStreamTimeoutException
        // statt still zu enden. Gezielt fangen -> ehrlicher Fehlschlag statt stiller Teilerfolg, und
        // der Wurf fliegt nicht ungefangen aus RunAsync (Schutz vor Abbruch eines ganzen Batchlaufs).
        try
        {
        if (useMultiModel)
        {
            // â”€â”€ Multi-Model Path: YOLO -> DINO -> SAM -> Qwen â”€â”€
            var pipelineClient = new VisionPipelineClient(pipelineCfg.SidecarUrl, _httpClient, pipelineCfg.SidecarToken, _sidecarTelemetry);

            // Create Qwen vision service for VSA-Code enrichment
            var ollamaClient = CreateOllamaClient();
            var qwenVision = new EnhancedVisionAnalysisService(ollamaClient, _cfg.VisionModel, _codeCatalog);

            var multiModel = new MultiModelAnalysisService(
                _pipelineTraceWriter,
                pipelineClient, pipelineCfg,
                _cfg.FfmpegPath ?? "ffmpeg",
                qwenVision: qwenVision,
                logger: _loggerFactory.CreateLogger<MultiModelAnalysisService>(),
                pipelineEnvironmentOptions: _pipelineEnvironmentOptions,
                processOutputs: _processOutputs,
                // Checkpoint-Journal (Resume) am Erzeugungsort des Multi-Model-Services,
                // gleiches Muster wie die uebbrigen hier gebauten Infrastruktur-Dienste.
                checkpointJournal: new AnalysisCheckpointJournal(
                    _loggerFactory.CreateLogger<AnalysisCheckpointJournal>()),
                sidecarRestart: _sidecarRestart);
            // Abgeschlossene Journale sicher begrenzen: nur streng lesbare, abgeschlossene,
            // aeltere Dateien. Laufende oder beschaedigte Journale bleiben immer erhalten.
            AnalysisCheckpointJournal.CleanupCompletedJournals(
                TelemetryPathResolver.Current, TimeSpan.FromDays(14),
                _loggerFactory.CreateLogger<AnalysisCheckpointJournal>());
            multiModel.FrameStepSeconds = request.FrameStepSeconds;
            multiModel.DedupWindowFrames = request.DedupWindowFrames;

            // Echte Haltungslaenge statt 50m-Annahme fuer die lineare Meter-Schaetzung
            if (request.ReachLengthM is > 0.0 and double reachLength)
            {
                multiModel.EstimatedReachLengthM = reachLength;
                _logger.LogInformation("Meter-Schaetzung nutzt echte Haltungslaenge: {Length:F2} m", reachLength);
            }

            videoResult = await multiModel.AnalyzeAsync(
                request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
        }
        else
        {
            // â”€â”€ Ollama-Only Path (existing behavior) â”€â”€
            var client = CreateOllamaClient();
            var videoService = VideoFullAnalysisService.Create(
                pipelineTraceWriter: _pipelineTraceWriter,
                client: client,
                visionModel: _cfg.VisionModel,
                ffmpegPath: _cfg.FfmpegPath ?? "ffmpeg",
                codeCatalog: _codeCatalog,
                logger: _loggerFactory.CreateLogger<VideoFullAnalysisService>(),
                processOutputs: _processOutputs);

            videoService.FrameStepSeconds = request.FrameStepSeconds;
            videoService.DedupWindowFrames = request.DedupWindowFrames;

            videoResult = await videoService.AnalyzeAsync(
                request.VideoPath, analysisProgress, ct).ConfigureAwait(false);
        }
        }
        catch (VideoFrameStreamTimeoutException ex)
        {
            _logger.LogWarning("Videoanalyse abgebrochen (ffmpeg-Haenger): {Reason}", ex.Message);
            return PipelineResult.Failed(
                $"Video-Frame-Extraktion haengt (ffmpeg): {ex.Message}");
        }

        if (!videoResult.IsSuccess)
            return PipelineResult.Failed($"Video-Analyse fehlgeschlagen: {videoResult.Error}");

        // befund-2: Ein degradierter Lauf (Sidecar-Ausfall mitten im Video) darf nicht wie ein
        // sauberes Ergebnis wirken — deutlich sichtbar machen statt still weiterzulaufen.
        if (videoResult.Degraded)
        {
            _logger.LogWarning("Videoanalyse degradiert: {Reason}", videoResult.DegradedReason);
            progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
                "WARNUNG: " + (videoResult.DegradedReason ?? "Analyse unvollstaendig (Sidecar-Ausfall)."),
                FramesDone: videoResult.FramesAnalyzed, FramesTotal: videoResult.FramesAnalyzed));
        }

        // Qualifikations-Hinweis (Phase 1): die Analyse laeuft bewusst weiter, ist aber als
        // NICHT qualitaetsgesichert gekennzeichnet — die Statusleiste zeigt das ehrlich an.
        if (videoResult.DetectorQualified == false)
        {
            _logger.LogWarning(
                "Videoanalyse ohne qualitaetsgesicherten Detektor: {Reason}",
                videoResult.DetectorQualificationReason ?? "Altmodell nicht qualifiziert.");
            progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
                "WARNUNG: YOLO nicht freigegeben – DINO/SAM laufen weiter; Ergebnis manuell pruefen.",
                FramesDone: videoResult.FramesAnalyzed, FramesTotal: videoResult.FramesAnalyzed));
        }

        // Unvollstaendigkeits-Hinweis (Skip-Quote > 10 %): ueber denselben WARNUNG-Pfad
        // wie der Degraded-Hinweis ausspielen — Ergebnis ist nutzbar, aber lueckenhaft.
        if (videoResult.Incomplete)
        {
            _logger.LogWarning("Videoanalyse unvollstaendig: mehr als 10 % der Frames fehlerbedingt uebersprungen.");
            progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
                "WARNUNG: Mehr als 10 % der Frames wurden fehlerbedingt uebersprungen – Ergebnis unvollstaendig.",
                FramesDone: videoResult.FramesAnalyzed, FramesTotal: videoResult.FramesAnalyzed));
        }

        progress?.Report(new PipelineProgress(PipelinePhase.VideoAnalysis, 100,
            $"{videoResult.Detections.Count} Schäden erkannt in {videoResult.FramesAnalyzed} Frames.",
            FramesDone: videoResult.FramesAnalyzed,
            FramesTotal: videoResult.FramesAnalyzed));

        // â”€â”€ Phase 2: Code-Mapping (mit bereits analysierten Detections) â”€â”€â”€â”€â”€â”€â”€
        // BUG 1.3 FIX: GenerateFromDetectionsAsync statt GenerateAsync
        // â†’ kein zweites AnalyzeAsync mehr!
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

        // BUG 1.3 FIX: Detections direkt Ã¼bergeben
        var genResult = await generator.GenerateFromDetectionsAsync(
            videoResult.Detections, genRequest, mappingProgress, ct).ConfigureAwait(false);

        if (!genResult.IsSuccess)
            return PipelineResult.Failed($"Code-Mapping fehlgeschlagen: {genResult.Error}");

        var resultWarnings = genResult.Warnings.ToList();
        if (videoResult.Degraded)
        {
            resultWarnings.Add(
                "Manuelle Pruefung erforderlich: "
                + (videoResult.DegradedReason ?? "Die Videoanalyse war eingeschraenkt."));
        }
        if (videoResult.Incomplete)
        {
            resultWarnings.Add(
                "Ergebnis unvollstaendig: mehr als 10 % der Frames wurden fehlerbedingt "
                + "uebersprungen (Sidecar-/Modellfehler). Manuelle Pruefung empfohlen.");
        }

        progress?.Report(new PipelineProgress(PipelinePhase.CodeMapping, 100,
            $"{genResult.MappedEntries.Count(e => e.SuggestedCode != null)} Einträge gemappt.",
            ItemsDone: genResult.MappedEntries.Count,
            ItemsTotal: genResult.MappedEntries.Count));

        progress?.Report(new PipelineProgress(
            PipelinePhase.Done,
            100,
            videoResult.Degraded
                ? "Fertig – Ergebnis ist eingeschraenkt und muss manuell geprueft werden."
                : videoResult.Incomplete
                    ? "Fertig – Ergebnis ist unvollstaendig (>10 % der Frames uebersprungen); manuelle Pruefung empfohlen."
                    : "Fertig."));

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
            Warnings: resultWarnings,
            Error: null,
            Telemetry: videoResult.Telemetry,
            Incomplete: videoResult.Incomplete);
    }

    /// <summary>
    /// Determines whether the Multi-Model pipeline should be used.
    /// - OllamaOnly: never use sidecar.
    /// - Auto: check sidecar health, use if available, fall back to Ollama otherwise.
    /// - MultiModel: require sidecar (error if not reachable).
    /// </summary>
    internal async Task<(bool UseMultiModel, PipelineConfig Config, string? FallbackReason)> ShouldUseMultiModelAsync(CancellationToken ct)
    {
        var pipelineCfg = _pipelineCfg;

        // OllamaOnly und Kill-Switch sind ABSICHTLICHE Modi -> kein Warn-Fallback.
        if (pipelineCfg.Mode == PipelineMode.OllamaOnly)
            return (false, pipelineCfg, null);

        // MultiModelEnabled ist ein Master-Kill-Switch.
        // Nur ein explizites Mode=MultiModel Ã¼bersteuert ihn.
        if (!pipelineCfg.MultiModelEnabled && pipelineCfg.Mode != PipelineMode.MultiModel)
            return (false, pipelineCfg, null);

        // Detaillierten Check verwenden: Nur dieser unterscheidet Offline, Token-Fehler
        // und eine unpassende Sidecar-Vertragsversion. Der einfache Health-Check darf
        // den Multi-Model-Hauptpfad nicht freigeben.
        try
        {
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, _httpClient, pipelineCfg.SidecarToken, _sidecarTelemetry);
            var healthCheck = await client.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
            var notReadyReason = DescribeSidecarNotReady(healthCheck, pipelineCfg.SidecarUrl);
            if (notReadyReason is not null)
            {
                if (pipelineCfg.Mode == PipelineMode.MultiModel)
                    throw new InvalidOperationException(
                        $"{notReadyReason}, aber PipelineMode=MultiModel erzwungen.");
                return (false, pipelineCfg,
                    $"{notReadyReason} - Analyse laeuft im schwaecheren Ollama-Only-Modus.");
            }

            // Klassifikator fehlt -> Warnung statt Blocker: Multi-Model laeuft weiter,
            // aber ohne VSA-Klassifikator-Codes (Sidecar meldet dann "degraded").
            if (healthCheck.Health?.Classifier is { Loaded: false })
                return (true, pipelineCfg,
                    "Sidecar-Klassifikator nicht geladen (Gewichte fehlen) - Analyse ohne Klassifikator-Codes.");

            var detectorQualification = healthCheck.Health?.DetectorQualification;
            if (detectorQualification?.Qualified != true)
                return (true, pipelineCfg,
                    "YOLO-Detektor nicht qualifiziert"
                    + (string.IsNullOrWhiteSpace(detectorQualification?.Reason)
                        ? ": Qualifikationsstatus fehlt oder ist unlesbar"
                        : $": {detectorQualification?.Reason}")
                    + " - DINO/SAM laufen ohne YOLO-Filter; Ergebnis muss manuell geprueft werden.");

            return (true, pipelineCfg, null);
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            if (pipelineCfg.Mode == PipelineMode.MultiModel)
                throw;
            return (false, pipelineCfg,
                $"Sidecar-Fehler ({ex.Message}) - Analyse laeuft im schwaecheren Ollama-Only-Modus.");
        }
    }

    private static string? DescribeSidecarNotReady(PipelineHealthCheckResult check, Uri sidecarUrl)
    {
        if (!check.IsReachable)
            return $"Sidecar nicht erreichbar ({sidecarUrl})";
        if (!check.IsAuthorized)
            return $"Sidecar-Token ungueltig oder fehlt ({sidecarUrl})";
        if (check.Health is null)
            return $"Sidecar-Health fehlgeschlagen: {check.Error ?? "keine gueltige Antwort"}";
        if (!string.IsNullOrWhiteSpace(check.Error))
            return check.Error;

        var health = check.Health;
        if (!health.HasRequiredModels)
            return $"Sidecar unvollstaendig: {health.MissingRequiredModelsText}-Gewichte fehlen";

        // Fehlender Klassifikator ist kein Blocker: die Warnung gibt der Aufrufer aus,
        // hier darf das "degraded" des Sidecars nicht als hartes Nicht-bereit zaehlen.
        if (health.ClassifierMissing)
            return null;

        // Ein unqualifizierter Detektor macht den Sidecar ehrlich "degraded", aber
        // DINO und SAM bleiben nutzbar. Deshalb nicht auf Ollama-only zurueckfallen.
        if (health.DetectorQualification?.Qualified != true)
            return null;

        return string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Sidecar meldet Status '{health.Status}'";
    }

    private OllamaClient CreateOllamaClient() => new(
        _cfg.OllamaBaseUri,
        _httpClient,
        _cfg.OllamaRequestTimeout,
        keepAlive: _cfg.OllamaKeepAlive,
        numCtx: _cfg.OllamaNumCtx);
}

// â”€â”€ Request / Result â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
