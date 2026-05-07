using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Maintenance;

/// <summary>
/// Sprint-1-Aufgabe: Versions-Pruning + Frame-Cleanup laufen ab jetzt
/// automatisch (nightly) statt nur per Diagnose-Tab-Klick. Beim App-Start
/// prueft der Scheduler ob seit dem letzten Lauf mindestens
/// <see cref="MinIntervalHours"/> Stunden vergangen sind, und stoesst dann
/// beide Wartungen im Hintergrund an.
///
/// Persistierung: <c>{LocalAppData}/SewerStudio/maintenance.json</c>.
///
/// Sicherheits-Defaults:
/// - FrameCleanup laeuft mit <see cref="FrameStoreCleanupService.DryRun"/>
///   = false (echte Bereinigung), aber dessen eigener Fail-Closed-Schutz
///   greift wenn die Sample-Liste leer/fehlerhaft ist.
/// - Bei jeder Exception wird der State NICHT aktualisiert, damit der
///   naechste App-Start es erneut versucht.
/// </summary>
public sealed class MaintenanceScheduler
{
    private readonly Func<CancellationToken, Task<FrameStoreCleanupResult>> _runFrameCleanup;
    private readonly Func<CancellationToken, Task<int>> _runVersionsPrune;
    private readonly Func<string> _getStateFilePath;

    /// <summary>
    /// Mindestintervall zwischen zwei Wartungslaeufen. Default 20 h
    /// (etwas unter 24 h, damit "naechste Nacht" nach 1 Tag App-Pause auch klappt).
    /// </summary>
    public int MinIntervalHours { get; set; } = 20;

    /// <summary>
    /// Verzoegerung beim App-Start, bevor der Scheduler losrennt. Default 5 Min —
    /// gibt der UI Zeit zu rendern, dem Sidecar Zeit zu starten, und Tests
    /// koennen es ueberschreiben.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Standard-Konstruktor mit echten Hooks. Aufrufer (App.xaml.cs)
    /// uebergibt die Hooks aus Infrastructure (KnowledgeBaseManager + FrameStoreCleanupService).
    /// </summary>
    public MaintenanceScheduler(
        Func<CancellationToken, Task<FrameStoreCleanupResult>> runFrameCleanup,
        Func<CancellationToken, Task<int>> runVersionsPrune)
        : this(runFrameCleanup, runVersionsPrune, DefaultStateFilePath)
    {
    }

    /// <summary>Test-Hook: Aufrufer kann eigenen State-Pfad uebergeben.</summary>
    public MaintenanceScheduler(
        Func<CancellationToken, Task<FrameStoreCleanupResult>> runFrameCleanup,
        Func<CancellationToken, Task<int>> runVersionsPrune,
        Func<string> getStateFilePath)
    {
        _runFrameCleanup = runFrameCleanup ?? throw new ArgumentNullException(nameof(runFrameCleanup));
        _runVersionsPrune = runVersionsPrune ?? throw new ArgumentNullException(nameof(runVersionsPrune));
        _getStateFilePath = getStateFilePath ?? throw new ArgumentNullException(nameof(getStateFilePath));
    }

    private static string DefaultStateFilePath()
        => PathConstants.InAppData(PathConstants.MaintenanceStateFile);

    /// <summary>Laedt den persistierten State. Liefert Default wenn Datei fehlt/korrupt.</summary>
    public MaintenanceState LoadState()
    {
        var path = _getStateFilePath();
        if (!File.Exists(path)) return new MaintenanceState();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MaintenanceState>(json) ?? new MaintenanceState();
        }
        catch
        {
            // Korrupte Datei → Default. Nicht kritisch, naechster Lauf ueberschreibt.
            return new MaintenanceState();
        }
    }

    /// <summary>Speichert den State.</summary>
    public void SaveState(MaintenanceState state)
    {
        var path = _getStateFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Prueft fuer beide Wartungen unabhaengig ob sie faellig sind, und fuehrt sie aus.
    /// Schreibt den State pro erfolgreichem Lauf einzeln, damit ein Fehler im einen
    /// Job nicht den anderen verzoegert.
    /// </summary>
    public async Task<MaintenanceRunResult> RunIfDueAsync(CancellationToken ct = default)
    {
        var startedUtc = DateTime.UtcNow;
        var state = LoadState();
        var errors = new List<string>();

        var dueFrameCleanup = IsDue(state.LastFrameCleanupUtc);
        var dueVersionsPrune = IsDue(state.LastVersionsPruneUtc);

        int framesDeleted = 0;
        long framesDeletedBytes = 0;
        int versionsPruned = 0;

        if (dueFrameCleanup)
        {
            try
            {
                var res = await _runFrameCleanup(ct).ConfigureAwait(false);
                framesDeleted = res.DeletedFiles;
                framesDeletedBytes = res.DeletedBytes;
                if (res.HasErrors())
                {
                    foreach (var e in res.Errors) errors.Add($"FrameCleanup: {e}");
                }
                // State erst bei erfolgreichem (auch fail-closed) Lauf aktualisieren.
                // Bei harter Exception wird der State unten nicht angefasst.
                state = state with
                {
                    LastFrameCleanupUtc = DateTime.UtcNow,
                    TotalFramesDeleted = state.TotalFramesDeleted + framesDeleted,
                };
                SaveState(state);
            }
            catch (OperationCanceledException)
            {
                throw; // Abbruch konsequent durchreichen.
            }
            catch (Exception ex)
            {
                errors.Add($"FrameCleanup: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (dueVersionsPrune)
        {
            try
            {
                versionsPruned = await _runVersionsPrune(ct).ConfigureAwait(false);
                state = state with
                {
                    LastVersionsPruneUtc = DateTime.UtcNow,
                    TotalVersionsPruned = state.TotalVersionsPruned + versionsPruned,
                };
                SaveState(state);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"VersionsPrune: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new MaintenanceRunResult(
            RanFrameCleanup: dueFrameCleanup,
            RanVersionsPrune: dueVersionsPrune,
            FramesDeleted: framesDeleted,
            FramesDeletedBytes: framesDeletedBytes,
            VersionsPruned: versionsPruned,
            StartedUtc: startedUtc,
            FinishedUtc: DateTime.UtcNow,
            Errors: errors);
    }

    /// <summary>
    /// Startet die Wartung im Hintergrund nach <see cref="StartupDelay"/>.
    /// Fire-and-Forget. Exceptions werden an <paramref name="onError"/> gemeldet.
    /// </summary>
    public Task StartBackgroundAsync(Action<Exception>? onError = null, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StartupDelay, ct).ConfigureAwait(false);
                await RunIfDueAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // App schliesst — normal.
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }, ct);
    }

    private bool IsDue(DateTime? lastRunUtc)
    {
        if (lastRunUtc == null) return true;
        return DateTime.UtcNow - lastRunUtc.Value >= TimeSpan.FromHours(MinIntervalHours);
    }
}

internal static class FrameStoreCleanupResultExtensions
{
    public static bool HasErrors(this FrameStoreCleanupResult r)
        => r.Errors != null && r.Errors.Count > 0;
}
