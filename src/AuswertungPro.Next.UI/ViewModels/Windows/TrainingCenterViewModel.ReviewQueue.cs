using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Ai.SelfImproving;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// TrainingCenterViewModel Review-Queue: Mittlere-Confidence-Samples (0.65-
// 0.85) durch User pruefen lassen — Approve/Reject + Self-Training-Pfad
// fuer wiederholtes Lernen aus den Reviews. Aus dem Hauptdatei extrahiert
// (Slice 13b).
public partial class TrainingCenterViewModel
{
    public void LoadReviewQueue(AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService queueService)
    {
        ReviewQueue.Clear();

        // Code-Frequenzen aus der Knowledge Base sammeln fuer Diversity-Sampling.
        IReadOnlyDictionary<string, int>? codeFrequencies = null;
        try
        {
            using var db = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT VsaCode, COUNT(*) FROM Samples WHERE VsaCode IS NOT NULL GROUP BY VsaCode";
            using var reader = cmd.ExecuteReader();
            var freqs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                var code = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (!string.IsNullOrEmpty(code)) freqs[code] = count;
            }
            codeFrequencies = freqs;
        }
        catch (Exception ex)
        {
            // Fallback: ohne Code-Frequenzen → Priority-only Selection
            System.Diagnostics.Debug.WriteLine(
                $"[ReviewQueue] Code-Frequenzen nicht verfuegbar, Active Learning faellt auf Priority zurueck: {ex.Message}");
        }

        // Auswahl: bis zu 50 Items via Active Learning (Uncertainty + Diversity).
        var items = queueService.GetTopForActiveLearning(50, codeFrequencies);
        foreach (var item in items)
            ReviewQueue.Add(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = codeFrequencies is not null
            ? $"{ReviewQueueCount} Einträge zur Prüfung (Active Learning, {codeFrequencies.Count} Codes in KB)"
            : $"{ReviewQueueCount} Einträge zur Prüfung";
    }

    /// <summary>Approve a review item (accept the suggested code).</summary>
    public async Task ApproveReviewItemAsync(
        AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem item,
        AuswertungPro.Next.Infrastructure.Ai.SelfImproving.FeedbackIngestionService feedback,
        AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, item.Entry.SuggestedCode ?? "", accepted: true, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            // Self-Training Review: Sample-Status auf Approved setzen + in KB indexieren
            await ApplySelfTrainingReviewAsync(
                item.SelfTrainingCaseId!, item.SelfTrainingVsaCode!,
                item.SelfTrainingMeter ?? 0, approved: true, correctedCode: null,
                sampleId: item.SelfTrainingSampleId, ct: ct);
        }
        // V4.2 Phase 1.5: TeacherAnnotation anhaengen fuer zukuenftiges Training.
        await TryAppendTeacherAnnotationAsync(item, item.SuggestedCode ?? item.SelfTrainingVsaCode ?? "", "approved");

        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Approved: {item.SuggestedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Approved: {item.Label} → {item.SuggestedCode}");
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem item,
        string correctedCode,
        AuswertungPro.Next.Infrastructure.Ai.SelfImproving.FeedbackIngestionService feedback,
        AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, correctedCode, accepted: false, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            // Self-Training Review: Sample-Status auf Rejected setzen, Code korrigieren
            await ApplySelfTrainingReviewAsync(
                item.SelfTrainingCaseId!, item.SelfTrainingVsaCode!,
                item.SelfTrainingMeter ?? 0, approved: false, correctedCode: correctedCode,
                sampleId: item.SelfTrainingSampleId, ct: ct);
        }
        // V4.2 Phase 1.5: TeacherAnnotation mit korrigiertem Code — besonders wertvoll!
        await TryAppendTeacherAnnotationAsync(item, correctedCode, "corrected");

        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Rejected: {item.SuggestedCode} → {correctedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Rejected: {item.Label} → {item.SuggestedCode} korrigiert zu {correctedCode}");
    }

    /// <summary>
    /// V4.2 Phase 1.5: Haengt eine TeacherAnnotation an den append-only Store, wenn Frame vorhanden.
    /// Review-Entscheidungen werden so zum Gold-Standard fuer zukuenftiges Training (inkl. DINOv2-Heads in Phase 3.2).
    /// </summary>
    private static async Task TryAppendTeacherAnnotationAsync(
        AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem item,
        string vsaCode,
        string reviewKind)
    {
        if (string.IsNullOrWhiteSpace(vsaCode)) return;

        // V4.3 Fix: Frame-Path darf null/leer sein — NoFindings-Items haben oft kein Frame,
        // und die Review-Entscheidung waere sonst verloren. TeacherAnnotation speichert dann
        // nur Code+Meter+Haltung als textueller Gold-Standard fuer FN-Reduktion.
        var framePath = item.SelfTrainingFramePath;
        var frameExists = !string.IsNullOrWhiteSpace(framePath) && System.IO.File.Exists(framePath);

        try
        {
            var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
            {
                VsaCode = vsaCode.Trim().ToUpperInvariant(),
                Beschreibung = $"Review-{reviewKind}: {item.Label}",
                MeterPosition = item.SelfTrainingMeter ?? 0.0,
                HaltungName = item.SelfTrainingCaseId,
                FullFramePath = frameExists ? framePath : null
            };
            await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);
        }
        catch
        {
            // TeacherAnnotation ist optionale Anreicherung — Review-Flow darf nicht an IO-Fehlern scheitern.
        }
    }

    /// <summary>
    /// Wendet eine Self-Training-Review-Entscheidung auf das TrainingSample an.
    /// Bei Approve: Status → Approved, inkrementelles KB-Update.
    /// Bei Reject: Status → Rejected (mit korrigiertem Code falls angegeben).
    /// </summary>
    private async Task ApplySelfTrainingReviewAsync(
        string caseId, string vsaCode, double meter,
        bool approved, string? correctedCode,
        string? sampleId = null, CancellationToken ct = default)
    {
        try
        {
            var allSamples = await TrainingSamplesStore.LoadAsync();
            // Primaer ueber SampleId suchen (eindeutig), Fallback fuer alte Queue-Eintraege
            var match = !string.IsNullOrEmpty(sampleId)
                ? allSamples.FirstOrDefault(s => s.SampleId == sampleId)
                : allSamples.FirstOrDefault(s =>
                    s.CaseId == caseId
                    && s.Code == vsaCode
                    && Math.Abs(s.MeterStart - meter) < 0.2);

            if (match is null)
            {
                // V4.3: Fuer NoFindings-Items existiert kein TrainingSample (KI hat nichts erkannt).
                // Das ist KEIN Fehler — die Review-Entscheidung wird trotzdem als TeacherAnnotation
                // (siehe TryAppendTeacherAnnotationAsync) persistiert.
                Log($"Self-Training Review: Kein KI-Sample zu aktualisieren ({caseId}/{vsaCode}@{meter:F1}m) — als TeacherAnnotation gespeichert");
                return;
            }

            if (approved)
            {
                match.Status = TrainingSampleStatus.Approved;
                match.KbIndexState = KbIndexState.Pending;
                match.MatchLevel = MatchLevelNames.ReviewApproved;
                Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Approved");

                // Inkrementell in KB indexieren
                var indexedIds = await IncrementalKbUpdateAsync(new List<TrainingSample> { match }, ct);
                match.KbIndexState = indexedIds.Contains(match.SampleId)
                    ? KbIndexState.Indexed
                    : KbIndexState.Error;
            }
            else
            {
                match.Status = TrainingSampleStatus.Rejected;
                if (!string.IsNullOrEmpty(correctedCode))
                {
                    Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Rejected, Code korrigiert zu {correctedCode}");
                    match.Notes = $"Korrigiert: {vsaCode} → {correctedCode}";

                    // Korrigiertes Sample als neues Trainingsbeispiel erzeugen
                    var corrected = new TrainingSample
                    {
                        SampleId = $"{match.SampleId}_corr",
                        CaseId = match.CaseId,
                        Code = correctedCode,
                        Beschreibung = match.Beschreibung,
                        MeterStart = match.MeterStart,
                        MeterEnd = match.MeterEnd,
                        IsStreckenschaden = match.IsStreckenschaden,
                        TimeSeconds = match.TimeSeconds,
                        DetectedMeter = match.DetectedMeter,
                        MeterSource = match.MeterSource,
                        FramePath = match.FramePath,
                        Status = TrainingSampleStatus.Approved,
                        KbIndexState = KbIndexState.Pending,
                        TruthMeterCenter = match.TruthMeterCenter,
                        OdsDeltaMeters = match.OdsDeltaMeters,
                        HasOsdMismatch = match.HasOsdMismatch,
                        Signature = TrainingSample.BuildCanonicalSignature(match.CaseId, correctedCode, match.MeterStart, match.MeterEnd),
                        MatchLevel = MatchLevelNames.ReviewCorrected,
                        SourceType = match.SourceType,
                        TechniqueGrade = match.TechniqueGrade,
                        KiCode = match.KiCode,
                        Notes = $"Korrektur aus Review: {vsaCode} → {correctedCode}"
                    };
                    // Korrigiertes Sample per Merge speichern (Race-Condition-sicher)
                    await TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { corrected });

                    // Inkrementell in KB indexieren
                    var corrIndexedIds = await IncrementalKbUpdateAsync(new List<TrainingSample> { corrected }, ct);
                    corrected.KbIndexState = corrIndexedIds.Contains(corrected.SampleId)
                        ? KbIndexState.Indexed
                        : KbIndexState.Error;
                    await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { corrected });

                    Log($"Korrigiertes Sample {corrected.SampleId} erzeugt, KB-Status: {corrected.KbIndexState}");
                }
                else
                {
                    Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Rejected");
                }
            }

            // Status-Aenderung von match (Approved/Rejected) atomar speichern.
            // MergeOrUpdateAsync statt SaveAsync: verhindert Ueberschreiben von parallel
            // geschriebenen Samples (z.B. corrected Sample aus L1569).
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { match });
            await LoadSamplesInternalAsync();
        }
        catch (Exception ex)
        {
            Log($"Self-Training Review Fehler: {ex.Message}");
        }
    }
}
