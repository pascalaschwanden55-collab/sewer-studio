using System.Diagnostics;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Liest und schreibt den UI-unabhaengigen Zustand des Training Centers.
/// Das bestehende JSON-, Backup- und Quarantaeneformat bleibt erhalten.
/// </summary>
public sealed class TrainingCenterDocumentFileStore : ITrainingCenterDocumentStore
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TrainingCenterDocumentFileStore(string? storeFilePath = null)
        => StoreFilePath = storeFilePath ?? GetDefaultStorePath();

    public string StoreFilePath { get; }

    public async Task<TrainingCenterDocument> LoadAsync()
    {
        try
        {
            if (!File.Exists(StoreFilePath))
                return new TrainingCenterDocument();

            await using var stream = File.OpenRead(StoreFilePath);
            var document = await JsonSerializer
                .DeserializeAsync<TrainingCenterDocument>(stream, JsonOptions)
                .ConfigureAwait(false);
            return document ?? new TrainingCenterDocument();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[TrainingCenterStore] Ladefehler: {ex.Message}");
            var badPath = StoreFilePath + ".bad_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            BestEffort.Try(
                () => File.Copy(StoreFilePath, badPath),
                $"Training-Center: korrupte Datei nach {badPath} sichern");

            var backupPath = StoreFilePath + ".bak";
            if (File.Exists(backupPath))
            {
                try
                {
                    await using var backupStream = File.OpenRead(backupPath);
                    var backup = await JsonSerializer
                        .DeserializeAsync<TrainingCenterDocument>(backupStream, JsonOptions)
                        .ConfigureAwait(false);
                    if (backup is not null)
                    {
                        Trace.WriteLine("[TrainingCenterStore] Backup .bak geladen");
                        return backup;
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
            return new TrainingCenterDocument();
        }
    }

    public async Task SaveAsync(TrainingCenterDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(document).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task SaveCoreAsync(TrainingCenterDocument document)
    {
        var directory = Path.GetDirectoryName(StoreFilePath)!;
        Directory.CreateDirectory(directory);

        if (File.Exists(StoreFilePath))
        {
            BestEffort.Try(
                () => File.Copy(StoreFilePath, StoreFilePath + ".bak", overwrite: true),
                "Training-Center: Sicherheitsbackup erstellen");
        }

        var tempPath = $"{StoreFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, document, JsonOptions)
                    .ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            await using (var checkStream = File.OpenRead(tempPath))
            {
                var check = await JsonSerializer
                    .DeserializeAsync<TrainingCenterDocument>(checkStream, JsonOptions)
                    .ConfigureAwait(false);
                if (check is null)
                {
                    throw new InvalidOperationException(
                        "Validierung fehlgeschlagen: temp-Datei nicht deserialisierbar");
                }
            }

            File.Move(tempPath, StoreFilePath, overwrite: true);
        }
        catch
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                },
                "Training-Center: Temp-Datei nach Speicherfehler loeschen");
            throw;
        }
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "training_center.json");
    }
}
