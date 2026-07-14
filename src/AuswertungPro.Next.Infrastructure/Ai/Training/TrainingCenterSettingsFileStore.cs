using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Dateibasierter Speicher fuer die Training-Center-Einstellungen.
/// Ein Instanz-Lock schuetzt paralleles Laden und Speichern.
/// </summary>
public sealed class TrainingCenterSettingsFileStore : ITrainingCenterSettingsStore
{
    private readonly Func<string> _storagePathProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public TrainingCenterSettingsFileStore()
        : this(() => KnowledgeBasePaths.GetTrainingSettingsPath())
    {
    }

    public TrainingCenterSettingsFileStore(string storagePath)
        : this(() => storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Der Speicherpfad darf nicht leer sein.", nameof(storagePath));
    }

    internal TrainingCenterSettingsFileStore(Func<string> storagePathProvider)
    {
        _storagePathProvider = storagePathProvider
            ?? throw new ArgumentNullException(nameof(storagePathProvider));
    }

    public string StoragePath => Path.GetFullPath(_storagePathProvider());

    public async Task<TrainingCenterSettings> LoadAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = StoragePath;
            if (!File.Exists(path))
                return new TrainingCenterSettings();

            try
            {
                await using var stream = File.OpenRead(path);
                var settings = await JsonSerializer
                    .DeserializeAsync<TrainingCenterSettings>(stream)
                    .ConfigureAwait(false);
                return settings ?? new TrainingCenterSettings();
            }
            catch (Exception ex)
            {
                var backup = path + $".bad_{DateTime.UtcNow:yyyyMMddHHmmss}";
                BestEffort.ReportWarning(
                    $"Training-Center-Einstellungen sind beschädigt ({path}): " +
                    $"{ex.GetType().Name}: {ex.Message}. Die Datei wird nach {backup} verschoben.");
                File.Move(path, backup, overwrite: true);
                return new TrainingCenterSettings();
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(TrainingCenterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonDefaults.Indented);
            await AtomicTextFileWriter
                .WriteAllTextAsync(StoragePath, json)
                .ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
