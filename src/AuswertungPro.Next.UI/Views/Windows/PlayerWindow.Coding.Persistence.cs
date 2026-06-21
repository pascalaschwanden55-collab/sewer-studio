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
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingTrainingFrameStore? _codingTrainingFrameStore;
    private CodingTrainingSampleEvalProtector? _codingTrainingSampleEvalProtector;
    private CodingTrainingSamplePersister? _codingTrainingSamplePersister;

    private CodingTrainingFrameStore CodingFrameStore
        => _codingTrainingFrameStore ??= new CodingTrainingFrameStore();

    private CodingTrainingSamplePersister CodingSamplePersister
        => _codingTrainingSamplePersister ??= new CodingTrainingSamplePersister(() => _codingSessionService);

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
            var framePath = CodingTrainingSampleFactory.PrimaryFramePath(ev);

            // Gold-Fund: Wenn der Befund kein eigenes Foto hat, aktuellen Frame als Snapshot sichern.
            // framePath bleibt bei Fehler null — das Speichern laeuft trotzdem durch (SnapshotError haelt den Grund fest).
            string? snapshotError = null;
            if (string.IsNullOrWhiteSpace(framePath))
            {
                var savedGoldFrame = await CodingFrameStore.SaveGoldFrameAsync(
                    ev,
                    _detectionPendingFrameBytes,
                    CaptureCurrentFrameAsync);
                framePath = savedGoldFrame.Path;
                snapshotError = savedGoldFrame.Error;
            }

            var evidenceFrame = CodingFrameStore.SaveEvidenceFrame(ev, framePath);
            if (evidenceFrame.Error != null)
                System.Diagnostics.Debug.WriteLine($"[Training] Beweisbild nicht gespeichert: {evidenceFrame.Error}");

            var sample = CodingTrainingSampleFactory.Create(
                ev,
                caseId,
                framePath,
                ResolveTrainingInspectionDate(),
                System.Environment.UserName,
                System.DateTime.UtcNow,
                evidenceFrame.Path,
                snapshotError);
            // Eval-Schutz (ESW-003): Frames/Haltungen aus dem eingefrorenen Eval-Set
            // niemals als Trainingssample speichern.
            if (IsCodingSampleEvalProtected(sample))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Training] Eval-Schutz: {sample.CaseId}/{sample.Code} NICHT als Gold gespeichert.");
                return;
            }

            await CodingSamplePersister.SaveAndIndexAsync(sample);
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
                CodingSamplePersister.SaveAndIndexAsync(samples).SafeFireAndForget("TrainingSave");
        }
        catch (Exception ex)
        {
            // Uebernahme darf nie blockiert werden, aber Fehler loggen
            System.Diagnostics.Debug.WriteLine($"[Training] Fehler: {ex.Message}");
        }
    }

    private DateTime? ResolveTrainingInspectionDate()
        => TrainingSampleEligibility.TryParseInspectionDate(_haltungRecord?.GetFieldValue("Datum_Jahr"));

    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m → Laenge_m → DamageOverlay → Protokoll BCE → manuelle Eingabe.
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
