using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

public sealed class TrainingCenterStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Verhindert gleichzeitige Load+Save-Operationen (analog zu TrainingSamplesStore)
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public string StoreFilePath { get; }

    public TrainingCenterStore(string? storeFilePath = null)
    {
        StoreFilePath = storeFilePath ?? GetDefaultStorePath();
    }

    public async Task<TrainingCenterState> LoadAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<TrainingCenterState> LoadInternalAsync()
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
            Debug.WriteLine($"[TrainingCenterStore] Ladefehler: {ex.Message}");

            // Backup der korrupten Datei (timestamped, nicht ueberschreiben)
            try
            {
                var badPath = StoreFilePath + ".bad_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                File.Copy(StoreFilePath, badPath);
            }
            catch { /* best-effort */ }

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
                        Debug.WriteLine("[TrainingCenterStore] Backup .bak geladen");
                        return bakState;
                    }
                }
                catch { /* Backup auch korrupt */ }
            }

            Debug.WriteLine("[TrainingCenterStore] WARNUNG: Kein lesbares Backup, starte mit leerem State");
            return new TrainingCenterState();
        }
    }

    /// <summary>
    /// Atomar speichern: temp-Datei + rename, mit Backup vor dem Schreiben.
    /// </summary>
    public async Task SaveAsync(TrainingCenterState state)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(state);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveInternalAsync(TrainingCenterState state)
    {
        var dir = Path.GetDirectoryName(StoreFilePath)!;
        Directory.CreateDirectory(dir);

        state.UpdatedUtc = DateTime.UtcNow;

        // Backup vor dem Schreiben
        if (File.Exists(StoreFilePath))
        {
            try { File.Copy(StoreFilePath, StoreFilePath + ".bak", overwrite: true); }
            catch { /* best-effort */ }
        }

        // In temp-Datei schreiben, dann atomar umbenennen
        var tempPath = StoreFilePath + ".tmp";
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
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best-effort */ }
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
