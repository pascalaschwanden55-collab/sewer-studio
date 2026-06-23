using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

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
            // framePath bleibt bei Fehler null - das Speichern laeuft trotzdem durch (SnapshotError haelt den Grund fest).
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
                PlayerClock.UtcNow(),
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
                    PlayerClock.UtcNow()));
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
}
