// AuswertungPro – Video-Selbsttraining: Voll-automatischer Batch-Betrieb
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

// BatchSelfTrainingOrchestrator pro-Haltung-Verarbeitung: ProcessSingleHaltung
// Async (Sequenziell mit Pause/Cancel-Support) und ProcessSingleHaltung
// ParallelAsync (mehrere Pipeline-Instanzen). Beinhalten: Protokoll-Lade-
// Versuche, Video-Pipeline-Aufruf, Differenz-Analyse, KB-Anreicherung.
// Aus dem Hauptdatei extrahiert (Slice 17a).
public sealed partial class BatchSelfTrainingOrchestrator
{
    private async Task<BatchHaltungResult> ProcessSingleHaltungAsync(
        DiscoveredHaltung h,
        BatchSelfTrainingRequest request,
        BatchAutoApprovePolicy policy,
        BatchKbStats stats,
        IProgress<BatchSelfTrainingProgress>? progress,
        int currentIndex,
        int total,
        CancellationToken ct,
        System.Collections.Concurrent.ConcurrentDictionary<string, PreloadedProtocol>? preloaded = null)
    {
        var hSw = Stopwatch.StartNew();

        // Phase 1: Protokoll aus Vorladen-Cache oder frisch laden
        Domain.Protocol.ProtocolDocument? protocol;
        Domain.Models.HaltungRecord? record;

        if (preloaded is not null && preloaded.TryGetValue(h.HaltungId, out var cached))
        {
            if (cached.Error is not null)
                return new BatchHaltungResult
                {
                    HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                    Success = false, Error = cached.Error,
                    Duration = hSw.Elapsed
                };
            protocol = cached.Protocol;
            record = cached.Record;
        }
        else
        {
            progress?.Report(new BatchSelfTrainingProgress
            {
                CurrentIndex = currentIndex, TotalHaltungen = total,
                HaltungId = h.HaltungId, Phase = "Import",
                Status = $"{h.HaltungId}: Protokoll importieren...",
                RunningStats = stats
            });
            (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
                h.ProtocolSource, h.SourceType, h.HaltungId);
        }

        if (protocol is null || protocol.Original.Entries.Count == 0)
        {
            var baseReason = protocol is null
                ? "PDF konnte nicht gelesen werden"
                : "PDF gelesen aber keine Protokoll-Eintraege gefunden";

            // V4.2 Nachbesserung: Diagnose anhaengen damit User im UI sieht warum.
            string diagnostic;
            try { diagnostic = Services.ProtocolLoaderFactory.DiagnosePdf(h.ProtocolSource); }
            catch (Exception ex) { diagnostic = $"Diag-Exception: {ex.Message}"; }

            var reason = $"{baseReason} — {diagnostic}";
            _log?.LogWarning("Batch: {Id}: {Reason} (PDF: {Pdf})",
                h.HaltungId, reason, Path.GetFileName(h.ProtocolSource));
            return new BatchHaltungResult
            {
                HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                Success = false, Error = reason,
                Duration = hSw.Elapsed
            };
        }

        var rohrmaterial = record?.GetFieldValue("Rohrmaterial");
        var dnText = record?.GetFieldValue("DN_mm");
        int? dn = int.TryParse(dnText, out var d) ? d : null;
        var hlText = record?.GetFieldValue("Haltungslaenge_m");
        double? inspLength = double.TryParse(
            hlText?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var hl) ? hl : null;

        // Phase 2: Video-Blinddurchlauf
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KI-Analyse",
            Status = $"{h.HaltungId}: Video wird analysiert...",
            RunningStats = stats
        });

        var trainingRequest = new VideoTrainingRequest
        {
            VideoPath = h.VideoPath,
            ProtocolSource = h.ProtocolSource,
            ProtocolSourceType = h.SourceType,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = dn,
            InspektionslaengeMeter = inspLength,
            FrameStepSeconds = request.FrameStepSeconds,
            MeterTolerance = request.MeterTolerance
        };

        // Video-Progress an Batch-Progress weiterleiten
        var videoProgress = progress is not null
            ? new Progress<VideoTrainingProgress>(vp =>
            {
                progress.Report(new BatchSelfTrainingProgress
                {
                    CurrentIndex = currentIndex,
                    TotalHaltungen = total,
                    HaltungId = h.HaltungId,
                    Phase = "KI-Analyse",
                    Status = $"{h.HaltungId}: {vp.Status} ({vp.Current}/{vp.Total})",
                    RunningStats = stats
                });
            })
            : null;

        // V4.2 Phase 2: Protokoll-First-Modus auch fuer den sequentiellen Pfad durchreichen.
        _videoOrchestrator.UseProtocolFirst = request.UseProtocolFirst;
        _videoOrchestrator.ProtocolFirstMeterTolerance = request.ProtocolFirstMeterTolerance;
        _videoOrchestrator.EnableSurpriseGapsPass = request.EnableSurpriseGapsPass;
        _videoOrchestrator.SurpriseGapsFrameStep = request.SurpriseGapsFrameStep;

        var trainingResult = await _videoOrchestrator.RunAsync(
            trainingRequest, protocol, videoProgress, ct).ConfigureAwait(false);

        var report = trainingResult.Report;

        // Statistik aktualisieren
        stats.TruePositives += report.TruePositiveCount;
        stats.FalseNegatives += report.FalseNegativeCount;
        stats.FalsePositives += report.FalsePositiveCount;
        stats.CodeMismatches += report.CodeMismatchCount;

        // Phase 3: Auto-Approve in KB
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KB-Update",
            Status = $"{h.HaltungId}: KB-Anreicherung (TP:{report.TruePositiveCount} FN:{report.FalseNegativeCount} MM:{report.CodeMismatchCount})...",
            RunningStats = stats
        });

        var enrichResult = await _enrichment.AutoEnrichFromReportAsync(
            report, rohrmaterial, dn, policy, ct, haltungId: h.HaltungId).ConfigureAwait(false);

        stats.KbIndexed += enrichResult.Indexed;
        stats.KbDeduplicated += enrichResult.Deduplicated;
        stats.KbSkipped += enrichResult.Skipped;
        stats.Errors += enrichResult.Errors;

        // V4.2 Phase 1.4: Unsicherste Items in Review-Queue fuer manuelles Labeln.
        var samplingResult = _uncertaintySampler?.EnqueueTopUncertain(h.HaltungId, report);

        // V4.2 Nachbesserung A: Strukturiertes Haltungs-Telemetrie-Log.
        _log?.LogInformation(
            "Haltung {Id} Telemetry | auto_approved={Auto} queued={Q} rejected_by_gate={Gate} " +
            "dedup={Dd} errors={Err} tp={TP} fn={FN} fp={FP} mm={MM} mean_prio={Mean:F3} max_prio={Max:F3}",
            h.HaltungId,
            enrichResult.Indexed,
            samplingResult?.Enqueued ?? 0,
            enrichResult.Skipped,
            enrichResult.Deduplicated,
            enrichResult.Errors,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            samplingResult?.MeanPriority ?? 0.0,
            samplingResult?.MaxPriority ?? 0.0);

        // Phase 4: YOLO-Trainingskandidaten generieren
        try
        {
            var yoloOutputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "KI_BRAIN", "yolo_training_candidates", h.HaltungId.Replace("/", "_"));

            var candidates = new List<Infrastructure.Import.WinCan.YoloTrainingDataGenerator.TrainingCandidate>();
            foreach (var entry in report.Entries)
            {
                if (string.IsNullOrEmpty(entry.FramePath) || !File.Exists(entry.FramePath))
                    continue;

                var opCode = entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.VsaCode ?? "";
                if (string.IsNullOrEmpty(opCode)) continue;

                var yoloClassId = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.GetYoloClassId(opCode);
                var kiLabel = entry.KiDetection?.VsaCode ?? entry.KiDetection?.Label ?? "";
                var kiConf = entry.KiDetection?.Confidence ?? 0;

                // Nachbar-Frame-Konsistenz: vereinfacht — wenn TP dann stabil
                bool neighborMatch = entry.Category == AuswertungPro.Next.Application.Ai.Training.Models.DifferenceCategory.TruePositive;

                var quality = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.ClassifyQuality(
                    opCode, kiLabel, kiConf, boxAreaNorm: 0.3, neighborMatch);

                // BBox aus KI-Detection (wenn vorhanden)
                double bx1 = entry.KiDetection?.BboxX1 ?? 0.1;
                double by1 = entry.KiDetection?.BboxY1 ?? 0.1;
                double bx2 = entry.KiDetection?.BboxX2 ?? 0.9;
                double by2 = entry.KiDetection?.BboxY2 ?? 0.9;

                // FP → Negativ-Beispiel (leeres Label)
                if (entry.Category == AuswertungPro.Next.Application.Ai.Training.Models.DifferenceCategory.FalsePositive)
                {
                    yoloClassId = -1;
                    quality = "green"; // FP-Frames sind wertvolle Negativ-Beispiele
                }

                candidates.Add(new Infrastructure.Import.WinCan.YoloTrainingDataGenerator.TrainingCandidate(
                    FramePath: entry.FramePath,
                    Quality: quality,
                    OperatorCode: opCode,
                    YoloClassId: yoloClassId,
                    YoloLabel: kiLabel,
                    YoloConfidence: kiConf,
                    BoxX1: bx1 * 640, BoxY1: by1 * 480,
                    BoxX2: bx2 * 640, BoxY2: by2 * 480,
                    ImgWidth: 640, ImgHeight: 480,
                    Reason: $"{entry.Category}: op={opCode} ki={kiLabel} conf={kiConf:F2}"));
            }

            if (candidates.Count > 0)
            {
                var genStats = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.SaveCandidates(
                    candidates, yoloOutputDir);
                _log?.LogInformation(
                    "Batch [{I}/{T}] {Id}: YOLO-Training G={G} Y={Y} R={R} Neg={N}",
                    currentIndex, total, h.HaltungId,
                    genStats.Green, genStats.Yellow, genStats.Red, genStats.Negatives);
            }
        }
        catch (Exception yoloGenEx)
        {
            _log?.LogWarning(yoloGenEx, "Batch [{I}/{T}] {Id}: YOLO-Kandidaten Fehler",
                currentIndex, total, h.HaltungId);
        }

        hSw.Stop();

        _log?.LogInformation(
            "Batch [{I}/{T}] {Id}: TP={TP} FN={FN} FP={FP} MM={MM}, KB +{KB} (Dedup {DD}) in {Dur:F0}s",
            currentIndex, total, h.HaltungId,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            enrichResult.Indexed, enrichResult.Deduplicated,
            hSw.Elapsed.TotalSeconds);

        return new BatchHaltungResult
        {
            HaltungId = h.HaltungId,
            VideoPath = h.VideoPath,
            Success = true,
            ProtocolEntries = protocol.Original.Entries.Count,
            KiDetections = report.TruePositiveCount + report.FalsePositiveCount + report.CodeMismatchCount,
            TruePositives = report.TruePositiveCount,
            FalseNegatives = report.FalseNegativeCount,
            FalsePositives = report.FalsePositiveCount,
            CodeMismatches = report.CodeMismatchCount,
            KbIndexed = enrichResult.Indexed,
            KbDeduplicated = enrichResult.Deduplicated,
            Duration = hSw.Elapsed
        };
    }

    /// <summary>
    /// Parallele Variante: Verarbeitet eine Haltung mit eigenem Orchestrator.
    /// Stats werden thread-safe via Lock aktualisiert.
    /// </summary>
    private async Task<BatchHaltungResult> ProcessSingleHaltungParallelAsync(
        DiscoveredHaltung h,
        BatchSelfTrainingRequest request,
        BatchAutoApprovePolicy policy,
        BatchKbStats stats,
        object statsLock,
        VideoSelfTrainingOrchestrator localOrch,
        IProgress<BatchSelfTrainingProgress>? progress,
        int currentIndex,
        int total,
        int parallelism,
        CancellationToken ct,
        System.Collections.Concurrent.ConcurrentDictionary<string, PreloadedProtocol>? preloaded = null)
    {
        var hSw = Stopwatch.StartNew();

        // Phase 1: Protokoll aus Vorladen-Cache oder frisch laden
        Domain.Protocol.ProtocolDocument? protocol;
        Domain.Models.HaltungRecord? record;

        if (preloaded is not null && preloaded.TryGetValue(h.HaltungId, out var cached))
        {
            if (cached.Error is not null)
                return new BatchHaltungResult
                {
                    HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                    Success = false, Error = cached.Error,
                    Duration = hSw.Elapsed
                };
            protocol = cached.Protocol;
            record = cached.Record;
        }
        else
        {
            (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
                h.ProtocolSource, h.SourceType, h.HaltungId);
        }

        if (protocol is null || protocol.Original.Entries.Count == 0)
        {
            var baseReason = protocol is null
                ? "PDF konnte nicht gelesen werden"
                : "PDF gelesen aber keine Protokoll-Eintraege gefunden";

            // V4.2 Nachbesserung: Diagnose anhaengen damit User im UI sieht warum.
            string diagnostic;
            try { diagnostic = Services.ProtocolLoaderFactory.DiagnosePdf(h.ProtocolSource); }
            catch (Exception ex) { diagnostic = $"Diag-Exception: {ex.Message}"; }

            var reason = $"{baseReason} — {diagnostic}";
            _log?.LogWarning("Batch: {Id}: {Reason} (PDF: {Pdf})",
                h.HaltungId, reason, Path.GetFileName(h.ProtocolSource));
            return new BatchHaltungResult
            {
                HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                Success = false, Error = reason,
                Duration = hSw.Elapsed
            };
        }

        var rohrmaterial = record?.GetFieldValue("Rohrmaterial");
        var dnText = record?.GetFieldValue("DN_mm");
        int? dn = int.TryParse(dnText, out var d) ? d : null;
        var hlText = record?.GetFieldValue("Haltungslaenge_m");
        double? inspLength = double.TryParse(
            hlText?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var hl) ? hl : null;

        // Phase 2: Video-Blinddurchlauf (mit eigenem Orchestrator)
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KI-Analyse",
            Status = $"[P{parallelism}] {h.HaltungId}: Video wird analysiert...",
            RunningStats = stats
        });

        var trainingRequest = new VideoTrainingRequest
        {
            VideoPath = h.VideoPath,
            ProtocolSource = h.ProtocolSource,
            ProtocolSourceType = h.SourceType,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = dn,
            InspektionslaengeMeter = inspLength,
            FrameStepSeconds = request.FrameStepSeconds,
            MeterTolerance = request.MeterTolerance
        };

        // Frame-Fortschritt an Batch-Progress weiterleiten
        var frameProgress = progress is not null
            ? new Progress<VideoTrainingProgress>(fp =>
                progress.Report(new BatchSelfTrainingProgress
                {
                    CurrentIndex = currentIndex, TotalHaltungen = total,
                    HaltungId = h.HaltungId, Phase = fp.Phase,
                    Status = $"[P{parallelism}] {h.HaltungId}: {fp.Status} ({fp.Current}/{fp.Total})",
                    RunningStats = stats
                }))
            : null;

        var trainingResult = await localOrch.RunAsync(
            trainingRequest, protocol, frameProgress, ct).ConfigureAwait(false);

        var report = trainingResult.Report;

        // Thread-sichere Statistik-Updates
        lock (statsLock)
        {
            stats.TruePositives += report.TruePositiveCount;
            stats.FalseNegatives += report.FalseNegativeCount;
            stats.FalsePositives += report.FalsePositiveCount;
            stats.CodeMismatches += report.CodeMismatchCount;
        }

        // Phase 3: KB-Anreicherung (KbEnrichmentService ist thread-safe dank SQLite-Transaktion)
        var enrichResult = await _enrichment.AutoEnrichFromReportAsync(
            report, rohrmaterial, dn, policy, ct, haltungId: h.HaltungId).ConfigureAwait(false);

        lock (statsLock)
        {
            stats.KbIndexed += enrichResult.Indexed;
            stats.KbDeduplicated += enrichResult.Deduplicated;
            stats.KbSkipped += enrichResult.Skipped;
            stats.Errors += enrichResult.Errors;
        }

        // V4.2 Phase 1.4: Unsicherste Items in Review-Queue fuer manuelles Labeln.
        var samplingResult = _uncertaintySampler?.EnqueueTopUncertain(h.HaltungId, report);

        // V4.2 Nachbesserung A: Strukturiertes Haltungs-Telemetrie-Log.
        _log?.LogInformation(
            "Haltung {Id} Telemetry | auto_approved={Auto} queued={Q} rejected_by_gate={Gate} " +
            "dedup={Dd} errors={Err} tp={TP} fn={FN} fp={FP} mm={MM} mean_prio={Mean:F3} max_prio={Max:F3}",
            h.HaltungId,
            enrichResult.Indexed,
            samplingResult?.Enqueued ?? 0,
            enrichResult.Skipped,
            enrichResult.Deduplicated,
            enrichResult.Errors,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            samplingResult?.MeanPriority ?? 0.0,
            samplingResult?.MaxPriority ?? 0.0);

        hSw.Stop();

        _log?.LogInformation(
            "Batch [{I}/{T}] {Id}: TP={TP} FN={FN} FP={FP} MM={MM}, KB +{KB} (Dedup {DD}) in {Dur:F0}s",
            currentIndex, total, h.HaltungId,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            enrichResult.Indexed, enrichResult.Deduplicated,
            hSw.Elapsed.TotalSeconds);

        return new BatchHaltungResult
        {
            HaltungId = h.HaltungId,
            VideoPath = h.VideoPath,
            Success = true,
            ProtocolEntries = protocol.Original.Entries.Count,
            KiDetections = report.TruePositiveCount + report.FalsePositiveCount + report.CodeMismatchCount,
            TruePositives = report.TruePositiveCount,
            FalseNegatives = report.FalseNegativeCount,
            FalsePositives = report.FalsePositiveCount,
            CodeMismatches = report.CodeMismatchCount,
            KbIndexed = enrichResult.Indexed,
            KbDeduplicated = enrichResult.Deduplicated,
            Duration = hSw.Elapsed
        };
    }
}
