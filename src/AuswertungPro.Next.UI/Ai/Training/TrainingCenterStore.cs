using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterStore
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string StoreFilePath { get; }

    public TrainingCenterStore(string? storeFilePath = null)
    {
        StoreFilePath = storeFilePath ?? GetDefaultStorePath();
    }

    public async Task<TrainingCenterState> LoadAsync()
    {
        try
        {
            if (!File.Exists(StoreFilePath))
                return new TrainingCenterState();

            await using var fs = File.OpenRead(StoreFilePath);
            var state = await JsonSerializer.DeserializeAsync<TrainingCenterState>(fs, JsonOptions);

            return state ?? new TrainingCenterState();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[TrainingCenterStore] Ladefehler: {ex.Message}");

            // Backup der korrupten Datei (timestamped, nicht ueberschreiben)
            var badPath = StoreFilePath + ".bad_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            BestEffort.Try(
                () => File.Copy(StoreFilePath, badPath),
                $"Training-Center: korrupte Datei nach {badPath} sichern");

            // Fallback auf .bak (juengstes Save-Backup)
            var bakPath = StoreFilePath + ".bak";
            if (File.Exists(bakPath))
            {
                try
                {
                    await using var bakFs = File.OpenRead(bakPath);
                    var bakState = await JsonSerializer.DeserializeAsync<TrainingCenterState>(bakFs, JsonOptions);
                    if (bakState is not null)
                    {
                        Trace.WriteLine("[TrainingCenterStore] Backup .bak geladen");
                        return bakState;
                    }
                }
                catch (Exception backupError)
                {
                    BestEffort.ReportWarning(
                        $"[TrainingCenterStore] Backup ebenfalls unlesbar: {backupError.Message}");
                }
            }

            BestEffort.ReportWarning(
                "[TrainingCenterStore] WARNUNG: Kein lesbares Backup, starte mit leerem State");
            return new TrainingCenterState();
        }
    }

    /// <summary>
    /// Atomar speichern: temp-Datei + rename, mit Backup vor dem Schreiben.
    /// </summary>
    public async Task SaveAsync(TrainingCenterState state)
    {
        await _saveLock.WaitAsync();
        try
        {
            await SaveCoreAsync(state);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task SaveCoreAsync(TrainingCenterState state)
    {
        var dir = Path.GetDirectoryName(StoreFilePath)!;
        Directory.CreateDirectory(dir);

        state.UpdatedUtc = DateTime.UtcNow;

        // Backup vor dem Schreiben
        if (File.Exists(StoreFilePath))
        {
            BestEffort.Try(
                () => File.Copy(StoreFilePath, StoreFilePath + ".bak", overwrite: true),
                "Training-Center: Sicherheitsbackup erstellen");
        }

        // In temp-Datei schreiben, dann atomar umbenennen
        var tempPath = $"{StoreFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(fs, state, JsonOptions);
                await fs.FlushAsync();
            }

            // Validierung: temp-Datei muss lesbar sein
            await using (var checkFs = File.OpenRead(tempPath))
            {
                var check = await JsonSerializer.DeserializeAsync<TrainingCenterState>(checkFs, JsonOptions);
                if (check is null)
                    throw new InvalidOperationException("Validierung fehlgeschlagen: temp-Datei nicht deserialisierbar");
            }

            File.Move(tempPath, StoreFilePath, overwrite: true);
        }
        catch
        {
            BestEffort.Try(
                () => { if (File.Exists(tempPath)) File.Delete(tempPath); },
                "Training-Center: Temp-Datei nach Speicherfehler loeschen");
            throw;
        }
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AuswertungPro");
        return Path.Combine(dir, "training_center.json");
    }
}
