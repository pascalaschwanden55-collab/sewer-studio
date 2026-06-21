using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Konvertiert die KI-Events aus dem Codiermodus in TrainingSamples
    /// und speichert sie via TrainingSamplesStore.
    /// Schliesst den Feedback-Loop im PlayerWindow (analog zu CodingSessionService.CompleteSession).
    /// </summary>
    /// <summary>
    /// Speichert ein einzelnes CodingEvent sofort als TrainingSample.
    /// Wird nach jeder Codierung aufgerufen â€” nicht erst beim Beenden.
    /// </summary>
    /// <summary>
    /// Sichert den aktuell analysierten Frame als Gold-Snapshot (PNG) unter knowledge/gold_frames,
    /// falls der Befund kein eigenes Foto hat. Liefert den Dateipfad oder eine Fehlermeldung zurueck.
    /// </summary>
    private async System.Threading.Tasks.Task<(string? path, string? error)> TrySaveGoldFrameAsync(CodingEvent ev)
    {
        try
        {
            var bytes = _detectionPendingFrameBytes;

            if (bytes == null || bytes.Length == 0)
                bytes = await CaptureCurrentFrameAsync();
            if (bytes == null || bytes.Length == 0)
                return (null, "kein Frame verfügbar");

            var dir = System.IO.Path.Combine(
                AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.GetRoot(), "gold_frames");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, $"{ev.EventId:N}.png");
            await System.IO.File.WriteAllBytesAsync(file, bytes);
            return (file, null);
        }
        catch (System.Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private (string? path, string? error) TrySaveEvidenceFrame(CodingEvent ev, string? rawFramePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawFramePath) || !System.IO.File.Exists(rawFramePath))
                return (null, "kein Rohbild für Beweisbild verfügbar");

            var dir = System.IO.Path.Combine(
                AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.GetRoot(),
                "gold_frames_annotated");
            var file = System.IO.Path.Combine(dir, $"{ev.EventId:N}_annotated.png");
            var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
                rawFramePath,
                file,
                CodingEvidenceAnnotationBuilder.Build(ev));

            return saved ? (file, null) : (null, "Beweisbild konnte nicht erstellt werden");
        }
        catch (System.Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private CodingTrainingSampleEvalProtector? _codingTrainingSampleEvalProtector;

    /// <summary>
    /// True, wenn das Sample aus dem eingefrorenen Eval-Set stammt (inhaltsgleicher Frame
    /// ODER reservierte Eval-Haltung). Solche Samples duerfen NIE ins Training (ESW-003),
    /// sonst messen Benchmarks keine Generalisierung mehr. Leere Eval-Saetze -> immer false.
    /// </summary>
    private bool IsCodingSampleEvalProtected(TrainingSample sample)
        => (_codingTrainingSampleEvalProtector ??= new CodingTrainingSampleEvalProtector(_serviceProvider?.Settings))
            .IsProtected(sample);

    private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample(CodingEvent ev)
    {
        if (ev.Entry == null || string.IsNullOrWhiteSpace(ev.Entry.Code)) return;
        try
        {
            var caseId = _codingVm?.HaltungName ?? "unknown";
            var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;

            // Gold-Fund: Wenn der Befund kein eigenes Foto hat, aktuellen Frame als Snapshot sichern.
            // framePath bleibt bei Fehler null â€” das Speichern laeuft trotzdem durch (SnapshotError haelt den Grund fest).
            string? snapshotError = null;
            if (string.IsNullOrWhiteSpace(framePath))
            {
                var (snapPath, snapErr) = await TrySaveGoldFrameAsync(ev);
                framePath = snapPath;
                snapshotError = snapErr;
            }

            var (evidenceFramePath, evidenceError) = TrySaveEvidenceFrame(ev, framePath);
            if (evidenceError != null)
                System.Diagnostics.Debug.WriteLine($"[Training] Beweisbild nicht gespeichert: {evidenceError}");

            var sample = CodingTrainingSampleFactory.Create(
                ev,
                caseId,
                framePath,
                ResolveTrainingInspectionDate(),
                System.Environment.UserName,
                System.DateTime.UtcNow,
                evidenceFramePath,
                snapshotError);
            // Eval-Schutz (ESW-003): Frames/Haltungen aus dem eingefrorenen Eval-Set
            // niemals als Trainingssample speichern.
            if (IsCodingSampleEvalProtected(sample))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Training] Eval-Schutz: {sample.CaseId}/{sample.Code} NICHT als Gold gespeichert.");
                return;
            }

            await InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { sample });

            // Robustes Gehirn: bestaetigtes Gold SOFORT in die KnowledgeBase.db indexieren und den
            // KbIndexState zurueckschreiben. Frueher endete dieser Pfad bei MergeAndSaveAsync —
            // das Sample war als Gold gespeichert, aber NIE in der KB (KbIndexState blieb None).
            // Nur Approved indexieren; abgelehnte/negative Samples bleiben aus der positiven KB raus.
            if (sample.Status == TrainingSampleStatus.Approved && _codingSessionService is not null)
                await _codingSessionService.IndexConfirmedSampleAsync(sample);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }

    private void PersistCodingEventsAsTrainingSamples()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0) return;
        try
        {
            var caseId = _codingVm.HaltungName ?? "unknown";
            var samples = new List<TrainingSample>();
            var inspectionDate = ResolveTrainingInspectionDate();
            foreach (var ev in _codingVm.Events)
            {
                samples.Add(CodingTrainingSampleFactory.Create(
                    ev,
                    caseId,
                    CodingTrainingSampleFactory.PrimaryFramePath(ev),
                    inspectionDate,
                    System.Environment.UserName,
                    System.DateTime.UtcNow));
            }
            // Eval-Schutz (ESW-003): reservierte Eval-Haltungen/-Frames aussortieren.
            samples = samples.Where(s => !IsCodingSampleEvalProtected(s)).ToList();
            if (samples.Count > 0)
                PersistAndIndexBatchAsync(samples).SafeFireAndForget("TrainingSave");
        }
        catch (Exception ex)
        {
            // Uebernahme darf nie blockiert werden, aber Fehler loggen
            System.Diagnostics.Debug.WriteLine($"[Training] Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Speichert eine ganze Charge bestaetigter Codier-Samples und indexiert die Approved-Samples
    /// danach in die KnowledgeBase.db (gemeinsamer Pfad ueber CodingSessionService). Frueher endete
    /// der Sammel-Uebernahmepfad bei MergeAndSaveAsync — die Befunde waren als Gold gespeichert, aber
    /// nie in der KB (KbIndexState blieb None). Robustes Gehirn: Bestaetigtes Gold landet immer in der KB.
    /// </summary>
    private async System.Threading.Tasks.Task PersistAndIndexBatchAsync(List<TrainingSample> samples)
    {
        await InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(samples);

        if (_codingSessionService is null)
            return;
        foreach (var s in samples)
        {
            if (s.Status == TrainingSampleStatus.Approved)
                await _codingSessionService.IndexConfirmedSampleAsync(s);
        }
    }

    private DateTime? ResolveTrainingInspectionDate()
        => TrainingSampleEligibility.TryParseInspectionDate(_haltungRecord?.GetFieldValue("Datum_Jahr"));

    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m â†’ Laenge_m â†’ DamageOverlay â†’ Protokoll BCE â†’ manuelle Eingabe.
    /// </summary>
    private void EnsureHaltungslaenge(HaltungRecord record)
    {
        if (CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, _damageOverlay?.PipeLengthMeters))
            return;

        // Letzter Fallback: Benutzer manuell fragen.
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Haltungslaenge konnte nicht ermittelt werden.\n" +
            "Bitte Haltungslaenge in Meter eingeben (z.B. 45.3):",
            "Haltungslaenge eingeben", "");

        if (!string.IsNullOrWhiteSpace(input))
        {
            var normalized = input.Trim().Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    val.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Manual, userEdited: true);
            }
        }
    }
}
