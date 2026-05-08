using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Roadmap P1.3: Provenance fuer Trainings-/Export-Runs.
///
/// Pattern wie <see cref="SelfTrainingHistoryStore"/>: atomares Schreiben
/// (temp -&gt; validate -&gt; rename), thread-safe per <see cref="SemaphoreSlim"/>.
///
/// Aufruf-Pattern in den Trainings-Pfaden:
/// <code>
///   var run = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.SelfTraining);
///   try {
///       // ... TrainingSample.TrainingRunId = run.RunId fuer alle erzeugten Samples ...
///       await TrainingRunsStore.CompleteRunAsync(run.RunId, samplesAffected: n);
///   } catch (Exception ex) {
///       await TrainingRunsStore.FailRunAsync(run.RunId, ex.Message);
///       throw;
///   }
/// </code>
///
/// Behaelt die letzten 200 Runs — aelter abschneiden, weil das Ziel
/// Regression-Detection im naheliegenden Zeitraum ist; Langzeit-Provenance
/// landet ggf. spaeter in der KB-DB.
/// </summary>
public static class TrainingRunsStore
{
    private const int MaxRetained = 200;

    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static string GetPath() => PathConstants.InKnowledgeRoot(PathConstants.TrainingRunsFile);

    /// <summary>Startet einen neuen Run und gibt den Eintrag zurueck.</summary>
    public static async Task<TrainingRun> BeginRunAsync(string trigger, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(trigger))
            throw new ArgumentException("trigger Pflicht.", nameof(trigger));

        var run = new TrainingRun(
            RunId: Guid.NewGuid().ToString("N"),
            StartedUtc: DateTime.UtcNow,
            Trigger: trigger,
            Status: TrainingRunStatus.Running,
            FinishedUtc: null,
            SamplesAffected: null,
            ErrorMessage: null,
            Notes: notes);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var runs = await LoadInternalAsync().ConfigureAwait(false);
            runs.Add(run);
            await TrimAndSaveAsync(runs).ConfigureAwait(false);
        }
        finally { _lock.Release(); }

        return run;
    }

    /// <summary>Schliesst einen Run als erfolgreich ab.</summary>
    public static Task CompleteRunAsync(string runId, int? samplesAffected = null, string? notes = null)
        => UpdateStatusAsync(runId, TrainingRunStatus.Succeeded, samplesAffected, errorMessage: null, notes);

    /// <summary>Schliesst einen Run als fehlgeschlagen ab.</summary>
    public static Task FailRunAsync(string runId, string errorMessage, int? samplesAffected = null)
        => UpdateStatusAsync(runId, TrainingRunStatus.Failed, samplesAffected, errorMessage, notes: null);

    /// <summary>Schliesst einen Run als abgebrochen ab.</summary>
    public static Task CancelRunAsync(string runId, string? notes = null)
        => UpdateStatusAsync(runId, TrainingRunStatus.Cancelled, samplesAffected: null, errorMessage: null, notes);

    /// <summary>Liefert alle persistierten Runs (chronologisch, oldest first).</summary>
    public static async Task<IReadOnlyList<TrainingRun>> LoadAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try { return await LoadInternalAsync().ConfigureAwait(false); }
        finally { _lock.Release(); }
    }

    private static async Task UpdateStatusAsync(
        string runId, TrainingRunStatus newStatus, int? samplesAffected, string? errorMessage, string? notes)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("runId Pflicht.", nameof(runId));

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var runs = await LoadInternalAsync().ConfigureAwait(false);
            var idx = runs.FindIndex(r => string.Equals(r.RunId, runId, StringComparison.Ordinal));
            if (idx < 0) return;   // best-effort: unbekannter Run -> no-op

            var prev = runs[idx];
            runs[idx] = prev with
            {
                Status = newStatus,
                FinishedUtc = DateTime.UtcNow,
                SamplesAffected = samplesAffected ?? prev.SamplesAffected,
                ErrorMessage = errorMessage ?? prev.ErrorMessage,
                Notes = notes ?? prev.Notes,
            };

            await TrimAndSaveAsync(runs).ConfigureAwait(false);
        }
        finally { _lock.Release(); }
    }

    private static async Task TrimAndSaveAsync(List<TrainingRun> runs)
    {
        if (runs.Count > MaxRetained)
            runs = runs.Skip(runs.Count - MaxRetained).ToList();
        await SaveInternalAsync(runs).ConfigureAwait(false);
    }

    private static async Task<List<TrainingRun>> LoadInternalAsync()
    {
        var path = GetPath();
        if (!File.Exists(path)) return new List<TrainingRun>();

        try
        {
            using var stream = File.OpenRead(path);
            var runs = await JsonSerializer.DeserializeAsync<List<TrainingRun>>(stream).ConfigureAwait(false);
            return runs ?? new List<TrainingRun>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrainingRunsStore] WARNUNG: JSON korrupt: {ex.Message}");
            try
            {
                var backup = path + $".corrupt_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                File.Copy(path, backup, overwrite: true);
            }
            catch { /* best-effort */ }
            return new List<TrainingRun>();
        }
    }

    private static async Task SaveInternalAsync(List<TrainingRun> runs)
    {
        var path = GetPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        if (File.Exists(path))
        {
            try { File.Copy(path, path + ".bak", overwrite: true); }
            catch { /* best-effort */ }
        }

        var tempPath = path + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, runs, JsonDefaults.Indented).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            using (var check = File.OpenRead(tempPath))
            {
                var loaded = await JsonSerializer.DeserializeAsync<List<TrainingRun>>(check).ConfigureAwait(false);
                if (loaded is null || loaded.Count != runs.Count)
                    throw new InvalidOperationException(
                        $"Validierung fehlgeschlagen: erwartet {runs.Count}, gelesen {loaded?.Count ?? 0}");
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort */ }
            throw;
        }
    }
}
