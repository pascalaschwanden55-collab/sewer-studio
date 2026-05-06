// AuswertungPro – Video-Selbsttraining Phase 3
using System;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Orchestriert die KB-Anreicherung aus den Review-Entscheidungen der Phase-2-UI.
/// Nur menschlich bestaetigte Korrekturen werden in die KB aufgenommen.
/// Kein automatisches Retraining — nur KB-Indexierung.
/// </summary>
public sealed class KbEnrichmentService
{
    private readonly KnowledgeBaseManager _kbManager;
    private readonly KbDeduplicationService _dedup;
    private readonly SampleQualityGateService _qualityGate;
    private readonly ReviewQueueService? _reviewQueue;
    private readonly ILogger? _log;

    public KbEnrichmentService(
        KnowledgeBaseManager kbManager,
        KbDeduplicationService dedup,
        ILogger? log = null,
        ReviewQueueService? reviewQueue = null)
    {
        _kbManager = kbManager ?? throw new ArgumentNullException(nameof(kbManager));
        _dedup = dedup ?? throw new ArgumentNullException(nameof(dedup));
        _qualityGate = new SampleQualityGateService();
        _reviewQueue = reviewQueue;
        _log = log;
    }

    /// <summary>
    /// Verarbeitet die Review-Entscheidungen und reichert die KB an.
    /// Nur Eintraege mit ReviewDecision != Pending/Ignored werden verarbeitet.
    /// </summary>
    public async Task<KbEnrichmentResult> EnrichFromReviewAsync(
        IReadOnlyList<DifferenceEntryViewModel> reviewedEntries,
        string? rohrmaterial,
        int? nennweiteMm,
        CancellationToken ct = default)
    {
        int indexed = 0, skipped = 0, deduplicated = 0, errors = 0;

        var actionable = reviewedEntries
            .Where(e => e.Decision is ReviewDecision.KiCorrect or ReviewDecision.ProtocolCorrect)
            .ToList();

        if (actionable.Count == 0)
            return new KbEnrichmentResult(0, reviewedEntries.Count, 0, 0);

        foreach (var entry in actionable)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var sample = BuildTrainingSample(entry, rohrmaterial, nennweiteMm);
                if (sample is null)
                {
                    skipped++;
                    continue;
                }

                // QualityGate bewerten — Red wird abgewiesen, Green/Yellow in KB
                var qgResult = _qualityGate.Evaluate(sample);
                sample.QualityGateLevel = qgResult.Grade.ToString();

                if (qgResult.Grade == Training.SampleQualityGrade.Red)
                {
                    _log?.LogDebug(
                        "QualityGate Red fuer {Code}: {Issues}",
                        sample.Code, string.Join("; ", qgResult.Issues));
                    skipped++;
                    continue;
                }

                // Pruefe ob Sample die Mindestqualitaet fuer KB-Indexierung hat
                if (!KnowledgeBaseManager.IsIndexWorthy(sample))
                {
                    _log?.LogDebug("Sample nicht indexwuerdig: {Code}", sample.Code);
                    skipped++;
                    continue;
                }

                // Dedup-Check
                var dedupResult = await _dedup.CheckAsync(
                    sample, sample.IsKorrigiert, ct).ConfigureAwait(false);

                if (dedupResult.IsAlreadyCovered)
                {
                    _log?.LogDebug(
                        "Sample {Code} bereits in KB abgedeckt (Similarity={Sim:F3})",
                        sample.Code, dedupResult.HighestSimilarity);
                    deduplicated++;
                    continue;
                }

                // In KB indexieren
                var success = await _kbManager.IndexSampleAsync(sample, ct).ConfigureAwait(false);
                if (success)
                {
                    indexed++;
                    _log?.LogInformation(
                        "KB-Eintrag erstellt: {Code} (Korrigiert={Korr}, Similarity={Sim:F3})",
                        sample.Code, sample.IsKorrigiert, dedupResult.HighestSimilarity);
                }
                else
                {
                    errors++;
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Fehler bei KB-Anreicherung fuer Eintrag");
                errors++;
            }
        }

        return new KbEnrichmentResult(indexed, skipped, deduplicated, errors);
    }

    /// <summary>
    /// Voll-automatische KB-Anreicherung aus einem DifferenceReport — OHNE manuelles Review.
    /// Das Protokoll ist die Ground-Truth. Treffer und Korrekturen werden direkt uebernommen.
    ///
    /// Verwendet fuer den Batch-Nachtbetrieb ueber 3000 Haltungen.
    /// </summary>
    public async Task<KbEnrichmentResult> AutoEnrichFromReportAsync(
        DifferenceReport report,
        string? rohrmaterial,
        int? nennweiteMm,
        BatchAutoApprovePolicy policy,
        CancellationToken ct = default,
        string? haltungId = null)
    {
        int indexed = 0, skipped = 0, deduplicated = 0, errors = 0, queuedForReview = 0;
        var samplesForStore = new List<TrainingSample>();

        foreach (var entry in report.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // H3: Confidence-Staffel — entscheidet vor dem Sample-Bau, ob das Item
            //   a) Auto-Approve (conf >= MinDetectionConfidence)        → weiter im Flow
            //   b) Review-Queue (MinConfidenceForReview <= conf < MinDetectionConfidence)
            //   c) Verwerfen    (conf < MinConfidenceForReview)
            // Gilt nur fuer TP/CodeMismatch (die haben eine KI-Detection mit Confidence).
            if (entry.Category is DifferenceCategory.TruePositive or DifferenceCategory.CodeMismatch
                && entry.KiDetection is not null)
            {
                var conf = entry.KiDetection.Confidence;
                if (conf < policy.MinConfidenceForReview)
                {
                    // Zu unsicher — verwerfen (wie bisher).
                    skipped++;
                    continue;
                }
                if (conf < policy.MinDetectionConfidence)
                {
                    // Unsicher, aber nicht hoffnungslos — menschliches Review.
                    TryEnqueueForReview(entry, haltungId);
                    queuedForReview++;
                    continue;
                }
                // conf >= MinDetectionConfidence → normaler Auto-Approve-Pfad.
            }

            // Entscheide automatisch basierend auf Kategorie + Policy
            var sample = BuildSampleFromDifference(entry, rohrmaterial, nennweiteMm, policy, haltungId);
            if (sample is null)
            {
                skipped++;
                continue;
            }

            // QualityGate bewerten — Red wird abgewiesen, Green/Yellow in KB
            var qgResult = _qualityGate.Evaluate(sample);
            sample.QualityGateLevel = qgResult.Grade.ToString();

            if (qgResult.Grade == Training.SampleQualityGrade.Red)
            {
                _log?.LogDebug(
                    "QualityGate Red fuer {Code}: {Issues}",
                    sample.Code, string.Join("; ", qgResult.Issues));
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sample.FramePath) && File.Exists(sample.FramePath))
                samplesForStore.Add(sample);

            try
            {
                if (!KnowledgeBaseManager.IsIndexWorthy(sample))
                {
                    sample.KbIndexState = KbIndexState.None;
                    skipped++;
                    continue;
                }

                var dedupResult = await _dedup.CheckAsync(
                    sample, sample.IsKorrigiert, ct).ConfigureAwait(false);

                if (dedupResult.IsAlreadyCovered)
                {
                    // Kein eigener KB-Eintrag — Sample ist von einem anderen abgedeckt.
                    // FRUEHER: KbIndexState = Indexed (gelogen, hat zu Inkonsistenz gefuehrt).
                    sample.KbIndexState = KbIndexState.Deduplicated;
                    deduplicated++;
                    continue;
                }

                var success = await _kbManager.IndexSampleAsync(sample, ct).ConfigureAwait(false);
                if (success)
                {
                    sample.KbIndexState = KbIndexState.Indexed;
                    indexed++;
                }
                else
                {
                    sample.KbIndexState = KbIndexState.Error;
                    errors++;
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug(ex, "Auto-Enrich Fehler fuer {Code}", sample.Code);
                sample.KbIndexState = KbIndexState.Error;
                errors++;
            }
        }

        if (samplesForStore.Count > 0)
        {
            try
            {
                await TrainingSamplesStore.MergeOrUpdateAsync(samplesForStore).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Auto-Enrich: Speichern in TrainingSamplesStore fehlgeschlagen");
            }
        }

        return new KbEnrichmentResult(indexed, skipped, deduplicated, errors)
        {
            QueuedForReview = queuedForReview
        };
    }

    /// <summary>
    /// H3: Schiebt einen Mittel-Confidence-DifferenceEntry in die Review-Queue.
    /// Wenn kein ReviewQueueService injiziert ist, wird der Eintrag nur geloggt.
    /// </summary>
    private void TryEnqueueForReview(DifferenceEntry entry, string? haltungId)
    {
        if (_reviewQueue is null)
        {
            _log?.LogDebug(
                "Confidence-Review: kein ReviewQueueService injiziert, Eintrag {Code} verworfen",
                entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.Label ?? "?");
            return;
        }

        var caseId = haltungId ?? $"batch-review-{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var protocolCode = entry.ProtocolEntry?.VsaCode ?? "";
        var suggestedCode = entry.KiDetection?.Label ?? protocolCode;
        var meter = entry.ProtocolEntry?.MeterStart ?? entry.KiDetection?.Meter ?? 0.0;
        var framePath = entry.FramePath ?? entry.KiDetection?.FramePath ?? "";
        var matchLevel = entry.Category == DifferenceCategory.CodeMismatch
            ? MatchLevelNames.Mismatch
            : MatchLevelNames.PartialMatch;

        // Deterministische SampleId: gleiches Item (Haltung+Code+Meter) wird nicht doppelt enqueued.
        var sampleId = $"{caseId}|{protocolCode}|{meter:F2}";

        try
        {
            _reviewQueue.EnqueueFromSelfTraining(
                caseId, protocolCode, suggestedCode,
                meter, framePath, matchLevel, sampleId);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Review-Queue Enqueue fehlgeschlagen fuer {Code}", protocolCode);
        }
    }

    /// <summary>
    /// Baut ein TrainingSample aus einem DifferenceEntry — automatisch, ohne Review.
    /// Das Protokoll gewinnt immer.
    /// </summary>
    private static TrainingSample? BuildSampleFromDifference(
        DifferenceEntry entry,
        string? rohrmaterial,
        int? nennweiteMm,
        BatchAutoApprovePolicy policy,
        string? haltungId = null)
    {
        string? code;
        string? beschreibung;
        string? framePath;
        double meterStart;
        bool isKorrigiert;

        switch (entry.Category)
        {
            case DifferenceCategory.TruePositive:
                // KI und Protokoll stimmen ueberein — sicherer Fall
                if (!policy.ApproveMatches) return null;
                // FIX: Protokoll hat immer Vorrang. Wenn ProtocolEntry fehlt (Grundgeruest-TP),
                // ueberspringen — nur echte Uebereinstimmungen mit Protokoll lernen.
                if (entry.ProtocolEntry is null) return null;
                code = entry.ProtocolEntry.VsaCode;
                beschreibung = entry.ProtocolEntry.Text ?? code;
                framePath = entry.FramePath ?? entry.KiDetection?.FramePath;
                meterStart = entry.ProtocolEntry.MeterStart;
                isKorrigiert = false;
                break;

            case DifferenceCategory.CodeMismatch:
                // KI hat falsch codiert — Protokoll ist korrekt → Korrektur (besonders wertvoll)
                if (!policy.ApproveCorrections) return null;
                code = entry.ProtocolEntry?.VsaCode; // Protokoll gewinnt
                beschreibung = entry.ProtocolEntry?.Text ?? code;
                framePath = entry.FramePath ?? entry.KiDetection?.FramePath;
                meterStart = entry.ProtocolEntry?.MeterStart ?? entry.KiDetection?.Meter ?? 0;
                isKorrigiert = true;
                break;

            case DifferenceCategory.FalseNegative:
                // KI hat uebersehen — Frame bei dem Meter zeigt den Schaden
                // NUR lernen wenn Frame tatsaechlich existiert (OSD-bestaetigt)
                // Bei linearer Interpolation ist der Frame oft falsch → kein Auto-Learn
                if (!policy.LearnFromMissed) return null;
                framePath = entry.FramePath;
                if (string.IsNullOrWhiteSpace(framePath) || !System.IO.File.Exists(framePath))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[KbEnrichment] FN Skip: kein Frame fuer {entry.ProtocolEntry?.VsaCode} @ {entry.ProtocolEntry?.MeterStart:F1}m");
                    return null;
                }
                code = entry.ProtocolEntry?.VsaCode;
                beschreibung = entry.ProtocolEntry?.Text ?? code;
                meterStart = entry.ProtocolEntry?.MeterStart ?? 0;
                isKorrigiert = true;
                break;

            case DifferenceCategory.FalsePositive:
                // KI halluziniert — NICHT lernen
                return null;

            default:
                return null;
        }

        if (string.IsNullOrWhiteSpace(code)) return null;

        // H3: Confidence-Gating ist nach AutoEnrichFromReportAsync verlagert (Staffel
        // Auto-Approve / Review-Queue / Verwerfen). BuildSample bekommt nur noch Items,
        // die bereits als Auto-Approve-faehig eingestuft wurden.

        // M10: Sekunden-Granularitaet verhindert Kollision bei mehreren Batches am selben Tag.
        var effectiveCaseId = haltungId ?? $"batch-auto-{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var signature = TrainingSample.BuildCanonicalSignature(
            effectiveCaseId,
            code,
            meterStart,
            meterStart,
            entry.ProtocolEntry?.ClockPosition ?? entry.KiDetection?.ClockPosition);

        var sample = new TrainingSample
        {
            SampleId = Guid.NewGuid().ToString("N"),
            CaseId = effectiveCaseId,
            Code = code,
            Beschreibung = beschreibung ?? code,
            MeterStart = meterStart,
            MeterEnd = entry.ProtocolEntry?.MeterEnd ?? meterStart,
            IsStreckenschaden = entry.ProtocolEntry?.IsStreckenschaden ?? false,
            TimeSeconds = entry.FrameTimeSeconds ?? 0,
            FramePath = framePath ?? "",
            Status = TrainingSampleStatus.Approved,
            MatchLevel = isKorrigiert
                ? MatchLevelNames.ReviewCorrected
                : MatchLevelNames.ReviewApproved,
            SourceType = SourceTypeNames.VideoTimestamp,
            IsKorrigiert = isKorrigiert,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = nennweiteMm,
            Signature = signature,
            KbIndexState = KbIndexState.Pending
        };

        TryApplyBboxFromDetection(sample, entry.KiDetection);
        return sample;
    }

    private static void TryApplyBboxFromDetection(TrainingSample sample, BlindDetection? detection)
    {
        if (detection?.BboxX1 is null || detection.BboxY1 is null || detection.BboxX2 is null || detection.BboxY2 is null)
            return;

        var x1 = Math.Clamp(Math.Min(detection.BboxX1.Value, detection.BboxX2.Value), 0.0, 1.0);
        var y1 = Math.Clamp(Math.Min(detection.BboxY1.Value, detection.BboxY2.Value), 0.0, 1.0);
        var x2 = Math.Clamp(Math.Max(detection.BboxX1.Value, detection.BboxX2.Value), 0.0, 1.0);
        var y2 = Math.Clamp(Math.Max(detection.BboxY1.Value, detection.BboxY2.Value), 0.0, 1.0);

        var width = x2 - x1;
        var height = y2 - y1;
        if (width <= 0.001 || height <= 0.001)
            return;

        sample.BboxXCenter = Math.Round((x1 + x2) / 2.0, 4);
        sample.BboxYCenter = Math.Round((y1 + y2) / 2.0, 4);
        sample.BboxWidth = Math.Round(width, 4);
        sample.BboxHeight = Math.Round(height, 4);
    }

    /// <summary>
    /// Baut ein TrainingSample aus einer Review-Entscheidung (manuelles Review).
    /// </summary>
    private static TrainingSample? BuildTrainingSample(
        DifferenceEntryViewModel entry,
        string? rohrmaterial,
        int? nennweiteMm)
    {
        // Bestimme den korrekten Code basierend auf der Entscheidung
        string? code;
        string? beschreibung;
        bool isKorrigiert;

        if (entry.Decision == ReviewDecision.KiCorrect)
        {
            // KI hat recht — deren Code verwenden
            code = entry.KiCode;
            beschreibung = entry.Entry.KiDetection?.Label ?? code;
            isKorrigiert = false;
        }
        else if (entry.Decision == ReviewDecision.ProtocolCorrect)
        {
            // Protokoll hat recht — KI lag falsch → Korrektur besonders wertvoll
            code = entry.ProtocolCode;
            beschreibung = entry.Entry.ProtocolEntry?.Freitext
                ?? entry.Entry.ProtocolEntry?.Text
                ?? code;
            isKorrigiert = true;
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(code))
            return null;

        var framePath = entry.FramePath ?? "";
        var meterStart = entry.ProtocolMeter ?? entry.KiMeter ?? 0;

        var sample = new TrainingSample
        {
            SampleId = Guid.NewGuid().ToString("N"),
            CaseId = $"video-review-{DateTime.UtcNow:yyyyMMdd}",
            Code = code,
            Beschreibung = beschreibung ?? code,
            MeterStart = meterStart,
            MeterEnd = meterStart,
            FramePath = framePath,
            Status = TrainingSampleStatus.Approved,
            MatchLevel = isKorrigiert
                ? MatchLevelNames.ReviewCorrected
                : MatchLevelNames.ReviewApproved,
            SourceType = SourceTypeNames.VideoTimestamp,
            IsKorrigiert = isKorrigiert,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = nennweiteMm,
            KbIndexState = KbIndexState.Pending
        };

        // Reviewer-Annotation (BoundingBox) uebernehmen wenn vorhanden
        if (entry.AnnotationBbox is { } bbox)
        {
            sample.BboxXCenter = bbox.XCenter;
            sample.BboxYCenter = bbox.YCenter;
            sample.BboxWidth = bbox.Width;
            sample.BboxHeight = bbox.Height;
        }

        return sample;
    }
}

/// <summary>Policy fuer voll-automatische KB-Anreicherung im Batch-Betrieb.</summary>
public sealed class BatchAutoApprovePolicy
{
    /// <summary>Treffer (KI + Protokoll stimmen ueberein) automatisch in KB.
    /// V4.2: Default false — stoppt KB-Vergiftung. Aktivierung nur mit Confidence-Gate.</summary>
    public bool ApproveMatches { get; init; } = false;

    /// <summary>Korrekturen (KI falsch, Protokoll richtig) NICHT automatisch in KB.
    /// PDF-OCR hat ~5-8% Fehler — blindes Uebernehmen vergiftet die KB.</summary>
    public bool ApproveCorrections { get; init; } = false;

    /// <summary>Uebersehene Schaeden (Frame extrahieren, als Beispiel speichern).
    /// V4.2: Default false — OSD-Frame-Mapping kann falsch sein, Schaden auf Frame nicht garantiert sichtbar.</summary>
    public bool LearnFromMissed { get; init; } = false;

    /// <summary>Minimale KI-Detection-Confidence fuer Auto-Approve (0.0 - 1.0).
    /// V4.2: Ab diesem Wert wird das Sample automatisch in die KB indexiert.
    /// Darunter: siehe MinConfidenceForReview. Aus BlindDetection.Confidence gelesen.</summary>
    public double MinDetectionConfidence { get; init; } = 0.85;

    /// <summary>H3: Unter-Schwelle fuer Review-Queue.
    /// conf in [MinConfidenceForReview, MinDetectionConfidence) → Review-Queue.
    /// conf &lt; MinConfidenceForReview → verwerfen.
    /// Default 0.65: Qwen-Ensemble liefert typisch 0.5-0.7; bei 0.65 stehen Chance:Risiko
    /// noch gut genug fuer menschliches Review.</summary>
    public double MinConfidenceForReview { get; init; } = 0.65;

    /// <summary>
    /// V4.2 Standard-Policy: Nichts automatisch. Stoppt KB-Vergiftung.
    /// Opt-in fuer Auto-Approve nur mit explizitem Flag + Confidence-Gate.
    /// </summary>
    public static BatchAutoApprovePolicy Default => new();

    /// <summary>
    /// Legacy V4.1 Policy: Alles automatisch ausser False Positives / Korrekturen.
    /// Nur fuer Rueckwaerts-Kompatibilitaet bei Migrationen — NICHT fuer neue Runs nutzen.
    /// </summary>
    public static BatchAutoApprovePolicy LegacyAutoApprove => new()
    {
        ApproveMatches = true,
        ApproveCorrections = false,
        LearnFromMissed = true,
        MinDetectionConfidence = 0.0
    };
}

/// <summary>Ergebnis der KB-Anreicherung.</summary>
public sealed record KbEnrichmentResult(
    int Indexed,
    int Skipped,
    int Deduplicated,
    int Errors)
{
    /// <summary>H3: Items mit mittlerer Confidence, in Review-Queue verschoben.</summary>
    public int QueuedForReview { get; init; }

    public int Total => Indexed + Skipped + Deduplicated + Errors + QueuedForReview;
}
