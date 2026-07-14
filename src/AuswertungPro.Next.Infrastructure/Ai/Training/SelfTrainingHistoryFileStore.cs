using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Dateibasierter Verlaufsspeicher. Behaelt die neuesten 20 Laeufe und serialisiert
/// den gesamten Lese-Aendern-Schreiben-Ablauf pro Instanz.
/// </summary>
public sealed class SelfTrainingHistoryFileStore : ISelfTrainingHistoryStore
{
    private readonly Func<string> _storagePathProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public SelfTrainingHistoryFileStore()
        : this(() => Path.Combine(KnowledgeBasePaths.GetRoot(), "selftraining_history.json"))
    {
    }

    public SelfTrainingHistoryFileStore(string storagePath)
        : this(() => storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Der Speicherpfad darf nicht leer sein.", nameof(storagePath));
    }

    internal SelfTrainingHistoryFileStore(Func<string> storagePathProvider)
    {
        _storagePathProvider = storagePathProvider
            ?? throw new ArgumentNullException(nameof(storagePathProvider));
    }

    public string StoragePath => Path.GetFullPath(_storagePathProvider());

    public async Task<List<SelfTrainingRunSnapshot>> LoadAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AppendRunAsync(SelfTrainingRunSnapshot run)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
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
            _fileLock.Release();
        }
    }

    private async Task<List<SelfTrainingRunSnapshot>> LoadInternalAsync()
    {
        var path = StoragePath;
        if (!File.Exists(path))
            return new List<SelfTrainingRunSnapshot>();

        try
        {
            await using var stream = File.OpenRead(path);
            var runs = await JsonSerializer
                .DeserializeAsync<List<SelfTrainingRunSnapshot>>(stream)
                .ConfigureAwait(false);
            return runs ?? new List<SelfTrainingRunSnapshot>();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[SelfTrainingHistoryFileStore] WARNUNG: JSON korrupt: {ex.Message}");
            var backup = path + $".corrupt_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            BestEffort.Try(
                () => File.Copy(path, backup, overwrite: true),
                $"Selbsttraining-Verlauf: korrupte Datei nach {backup} sichern");
            return new List<SelfTrainingRunSnapshot>();
        }
    }

    private async Task SaveInternalAsync(List<SelfTrainingRunSnapshot> runs)
    {
        var path = StoragePath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        if (File.Exists(path))
        {
            BestEffort.Try(
                () => File.Copy(path, path + ".bak", overwrite: true),
                "Selbsttraining-Verlauf: Sicherheitsbackup erstellen");
        }

        var tempPath = path + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, runs, JsonDefaults.Indented)
                    .ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            await using (var check = File.OpenRead(tempPath))
            {
                var loaded = await JsonSerializer
                    .DeserializeAsync<List<SelfTrainingRunSnapshot>>(check)
                    .ConfigureAwait(false);
                if (loaded is null || loaded.Count != runs.Count)
                {
                    throw new InvalidOperationException(
                        $"Validierung fehlgeschlagen: erwartet {runs.Count}, gelesen {loaded?.Count ?? 0}");
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                },
                "Selbsttraining-Verlauf: Temp-Datei nach Speicherfehler loeschen");
            throw;
        }
    }
}
