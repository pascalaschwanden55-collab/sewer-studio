using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// TrainingCenterViewModel Sample-Operations: Loading + Generating + Bulk-
// Approve/Reject/Delete + Persist + Ollama-Reachability-Check fuer Training-
// Samples. Aus dem Hauptdatei extrahiert (Slice 35).
public partial class TrainingCenterViewModel
{
    [RelayCommand]
    private async Task LoadSamplesAsync()
    {
        await LoadSamplesInternalAsync();
    }

    private async Task LoadSamplesInternalAsync()
    {
        var list = await TrainingSamplesStore.LoadAsync();
        Samples.Clear();
        // Rejected-Samples ausblenden — nur New + Approved anzeigen
        foreach (var s in list.Where(s => s.Status != TrainingSampleStatus.Rejected))
            Samples.Add(s);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task GenerateSamplesAsync()
    {
        if (SelectedCase is null || IsBusy) return;

        var ct = RotateGenCts();

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            StatusText = $"Generiere Samples für {SelectedCase.CaseId}...";

            var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var existing = await TrainingSamplesStore.LoadAsync();
            var existingSigs = existing.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);

            var generation = await generator.GenerateWithDiagnosticsAsync(
                SelectedCase.Model, existingSigs, framesDir: null, ct);
            var newSamples = generation.Samples;

            if (newSamples.Count == 0)
            {
                StatusText = generation.Outcome switch
                {
                    TrainingSampleGenerationOutcome.OnlyDuplicates
                        => $"Keine neuen Samples für {SelectedCase.CaseId} (alle {generation.ParsedEntries} Einträge bereits vorhanden).",
                    TrainingSampleGenerationOutcome.NoProtocolEntries
                        => $"Keine Protokolleinträge erkannt für {SelectedCase.CaseId}.",
                    TrainingSampleGenerationOutcome.ProtocolUnreadable
                        => $"Protokoll konnte nicht gelesen werden: {SelectedCase.ProtocolPath}",
                    TrainingSampleGenerationOutcome.ProtocolFileMissing
                        => $"Protokolldatei fehlt: {SelectedCase.ProtocolPath}",
                    _ => "Keine neuen Samples generiert."
                };
                return;
            }

            await TrainingSamplesStore.MergeAndSaveAsync(newSamples);

            foreach (var s in newSamples)
                Samples.Add(s);

            StatusText = $"{newSamples.Count} neue Samples generiert für {SelectedCase.CaseId}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sample-Generierung abgebrochen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler bei Sample-Generierung: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSampleSelection() => SelectedSample is not null;

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task ApproveSampleAsync()
    {
        if (SelectedSample is null) return;
        var current = SelectedSample;
        var idx = Samples.IndexOf(current);
        current.Status = TrainingSampleStatus.Approved;
        StatusText = $"Approved: {current.Code} @ {current.MeterStart:F1}m";
        await PersistSamplesAsync(current);

        // Zum naechsten Sample springen
        Samples.Remove(current);
        if (Samples.Count > 0)
            SelectedSample = Samples[Math.Min(idx, Samples.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        if (SelectedSample is null) return;
        var current = SelectedSample;
        var idx = Samples.IndexOf(current);
        current.Status = TrainingSampleStatus.Rejected;
        StatusText = $"Rejected: {current.Code} @ {current.MeterStart:F1}m";
        await PersistSamplesAsync();

        // Eintrag entfernen und zum naechsten springen
        Samples.Remove(current);
        if (Samples.Count > 0)
            SelectedSample = Samples[Math.Min(idx, Samples.Count - 1)];
    }

    [RelayCommand]
    private async Task RejectAllVisibleAsync()
        => await BulkChangeVisibleStatusAsync(TrainingSampleStatus.Rejected, "Reject", "#DC2626");

    /// <summary>
    /// Setzt alle in der DataGrid markierten Samples auf Approved.
    /// SelectedItems wird via CommandParameter aus dem View uebergeben.
    /// </summary>
    [RelayCommand]
    private async Task ApproveSelectedAsync(System.Collections.IList? selected)
    {
        if (IsBusy || selected is null) return;
        var list = selected.Cast<TrainingSample>().ToList();
        if (list.Count == 0) { StatusText = "Keine Zeilen markiert."; return; }
        try
        {
            IsBusy = true;
            foreach (var s in list)
            {
                if (s.Status == TrainingSampleStatus.Approved) continue;
                s.Status = TrainingSampleStatus.Approved;
                if (s.KbIndexState != KbIndexState.Indexed
                    && s.KbIndexState != KbIndexState.Deduplicated)
                {
                    s.KbIndexState = KbIndexState.Pending;
                }
            }
            await PersistSamplesAsync();
            RefreshSamplesView();
            StatusText = $"{list.Count} markierte Samples approved.";
            Log($"Selection-Approve: {list.Count} Samples");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Loescht alle in der DataGrid markierten Samples HART aus dem JSON.
    /// Konfirmation Pflicht — destruktiv, kein Undo.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedAsync(System.Collections.IList? selected)
    {
        if (IsBusy || selected is null) return;
        var list = selected.Cast<TrainingSample>().ToList();
        if (list.Count == 0) { StatusText = "Keine Zeilen markiert."; return; }

        var confirm = _dialogs.ShowMessage(
            $"{list.Count} Samples werden ENDGUELTIG aus dem Training-Store geloescht.\n\n" +
            $"Frame-Dateien bleiben auf Disk. KB-Eintraege werden NICHT geloescht.\n\nFortfahren?",
            "Markierte Samples loeschen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var idsToRemove = new HashSet<string>(list.Select(s => s.SampleId));
            foreach (var s in list)
                Samples.Remove(s);

            await TrainingSamplesStore.RemoveByIdsAsync(idsToRemove);
            RefreshSamplesView();
            StatusText = $"{list.Count} markierte Samples geloescht.";
            Log($"Selection-Delete: {list.Count} Samples hart aus JSON entfernt");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Bulk-Approve: alle Samples die aktuell durch den Filter (Code + Status) sichtbar sind
    /// werden auf Approved gesetzt. Mit Konfirmations-Dialog wegen Massenwirkung.
    /// </summary>
    [RelayCommand]
    private async Task ApproveAllVisibleAsync()
        => await BulkChangeVisibleStatusAsync(TrainingSampleStatus.Approved, "Approve", "#16A34A");

    /// <summary>
    /// Setzt alle aktuell durch den Filter sichtbaren Pending-Samples auf den gewaehlten Status.
    /// Konfirmations-Dialog mit Top-3-Code-Summary.
    /// </summary>
    private async Task BulkChangeVisibleStatusAsync(
        TrainingSampleStatus newStatus, string actionLabel, string colorHint)
    {
        if (IsBusy) return;
        var visible = SamplesView?.Cast<TrainingSample>().ToList() ?? new();
        var pendingOnly = visible.Where(s => s.Status == TrainingSampleStatus.New).ToList();
        if (pendingOnly.Count == 0)
        {
            StatusText = "Keine Pending-Samples im aktuellen Filter sichtbar.";
            return;
        }

        var topCodes = pendingOnly
            .GroupBy(s => s.Code ?? "")
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}: {g.Count()}");
        var codeSummary = string.Join(", ", topCodes);

        var confirm = _dialogs.ShowMessage(
            $"{pendingOnly.Count} Pending-Samples werden auf {newStatus} gesetzt.\n\n" +
            $"Top-Codes: {codeSummary}\n\n" +
            $"Stichprobe vorher pruefen!\n\nFortfahren?",
            $"Bulk-{actionLabel}",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            StatusText = $"Bulk-{actionLabel} laeuft: {pendingOnly.Count} Samples...";

            foreach (var s in pendingOnly)
            {
                s.Status = newStatus;
                // KbIndexState nur setzen wenn noch nicht indexiert — sonst ueberschreiben wir
                // einen bereits erfolgreichen KB-Eintrag und machen die JSON-Statistik unwahr.
                if (newStatus == TrainingSampleStatus.Approved
                    && s.KbIndexState != KbIndexState.Indexed
                    && s.KbIndexState != KbIndexState.Deduplicated)
                {
                    s.KbIndexState = KbIndexState.Pending;
                }
            }
            await PersistSamplesAsync();

            RefreshSamplesView();
            var hint = newStatus == TrainingSampleStatus.Approved
                ? " Klicke 'KB nachindexieren' um sie in die KB zu schreiben."
                : "";
            StatusText = $"Bulk-{actionLabel} fertig: {pendingOnly.Count} Samples auf {newStatus} gesetzt.{hint}";
            Log($"Bulk-{actionLabel}: {pendingOnly.Count} Samples (Codes: {codeSummary})");
        }
        finally
        {
            IsBusy = false;
        }
    }



    /// <summary>
    /// Speichert alle Samples und indexiert optional ein gerade geaendertes Sample in die KB.
    /// </summary>
    private async Task PersistSamplesAsync(TrainingSample? changedSample = null)
    {
        // Immer Merge/Update statt Voll-Save — verhindert Ueberschreiben
        // von parallel geschriebenen Samples (Batch-Import, Self-Training).
        // V4.3: komplette Persistenz-Kette in try/catch — ein Lock-Fehler in
        // TrainingSamplesStore darf die App nicht abstuerzen lassen.
        try
        {
            if (changedSample != null)
                await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
            else
                await TrainingSamplesStore.MergeOrUpdateAsync(Samples.ToList());

            // Approved Sample sofort in KB indexieren ("sofort in die Datenbank")
            if (changedSample?.Status == TrainingSampleStatus.Approved)
            {
                changedSample.KbIndexState = KbIndexState.Pending;
                await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
                var indexedIds = await IncrementalKbUpdateAsync(
                    new List<TrainingSample> { changedSample },
                    CancellationToken.None);
                changedSample.KbIndexState = indexedIds.Contains(changedSample.SampleId)
                    ? KbIndexState.Indexed
                    : KbIndexState.Error;
                await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Speichern fehlgeschlagen: {ex.Message}";
            Log($"[Persist] FEHLER: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Prüft ob Ollama erreichbar ist (GET /api/tags).
    /// </summary>
    private static async Task<bool> CheckOllamaReachableAsync(OllamaConfig config, CancellationToken ct)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
