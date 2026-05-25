// AuswertungPro – Verlauf der Selbsttraining-Ergebnisse (Match-Rate pro Lauf)
// Atomares Speichern: temp-Datei → Validierung → File.Move (wie TrainingSamplesStore)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Persistiert Match-Rate-Verlaeufe im Knowledge-Ordner (portabel).
/// Thread-safe + atomares Speichern (Write-Replace-Pattern).
/// </summary>
public static class SelfTrainingHistoryStore
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static string GetPath()
        => Path.Combine(KnowledgeBasePaths.GetRoot(), "selftraining_history.json");

    public static async Task<List<SelfTrainingRunSnapshot>> LoadAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<List<SelfTrainingRunSnapshot>> LoadInternalAsync()
    {
        var path = GetPath();
        if (!File.Exists(path))
            return new List<SelfTrainingRunSnapshot>();

        try
        {
            using var stream = File.OpenRead(path);
            var runs = await JsonSerializer.DeserializeAsync<List<SelfTrainingRunSnapshot>>(stream)
                .ConfigureAwait(false);
            return runs ?? new List<SelfTrainingRunSnapshot>();
        }
        catch (Exception ex)
        {
            // Korrupte Datei sichern, nicht loeschen
            Debug.WriteLine($"[SelfTrainingHistoryStore] WARNUNG: JSON korrupt: {ex.Message}");
            try
            {
                var backup = path + $".corrupt_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                File.Copy(path, backup, overwrite: true);
                Debug.WriteLine($"[SelfTrainingHistoryStore] Backup unter: {backup}");
            }
            catch { /* best-effort */ }

            return new List<SelfTrainingRunSnapshot>();
        }
    }

    /// <summary>Atomar speichern: temp → Validierung → File.Move.</summary>
    private static async Task SaveInternalAsync(List<SelfTrainingRunSnapshot> runs)
    {
        var path = GetPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        // Sicherheits-Backup vor Schreiben
        if (File.Exists(path))
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch { /* best-effort */ }
        }

        // In temp-Datei schreiben
        var tempPath = path + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, runs, Application.Common.JsonDefaults.Indented)
                    .ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            // Validierung
            using (var check = File.OpenRead(tempPath))
            {
                var loaded = await JsonSerializer.DeserializeAsync<List<SelfTrainingRunSnapshot>>(check)
                    .ConfigureAwait(false);
                if (loaded is null || loaded.Count != runs.Count)
                    throw new InvalidOperationException(
                        $"Validierung fehlgeschlagen: erwartet {runs.Count}, gelesen {loaded?.Count ?? 0}");
            }

            // Atomares Rename
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best-effort */ }
            throw;
        }
    }

    /// <summary>Fuegt einen Lauf hinzu, behaelt max. 20 Eintraege. Thread-safe + atomar.</summary>
    public static async Task AppendRunAsync(SelfTrainingRunSnapshot run)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var runs = await LoadInternalAsync().ConfigureAwait(false);
            runs.Add(run);
            if (runs.Count > 20)
                runs = runs.Skip(runs.Count - 20).ToList();
            await SaveInternalAsync(runs).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }
}
