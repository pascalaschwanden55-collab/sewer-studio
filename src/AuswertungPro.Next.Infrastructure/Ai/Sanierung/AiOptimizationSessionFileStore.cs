using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Sanierung;

/// <summary>
/// Dateibasierter Speicher fuer KI-Sanierungssitzungen. Speichern ersetzt Sitzungen mit
/// gleicher ID und serialisiert parallele Lese-/Schreibvorgaenge pro Instanz.
/// </summary>
public sealed class AiOptimizationSessionFileStore : IAiOptimizationSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Func<string> _storagePathProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public AiOptimizationSessionFileStore()
        : this(() => Path.Combine(
            AppDataPathResolver.Resolve(),
            "ai_sanierung_sessions.json"))
    {
    }

    public AiOptimizationSessionFileStore(string storagePath)
        : this(() => storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Der Speicherpfad darf nicht leer sein.", nameof(storagePath));
    }

    internal AiOptimizationSessionFileStore(Func<string> storagePathProvider)
    {
        _storagePathProvider = storagePathProvider
            ?? throw new ArgumentNullException(nameof(storagePathProvider));
    }

    public string StoragePath => Path.GetFullPath(_storagePathProvider());

    public async Task SaveAsync(AiOptimizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var all = await LoadCoreAsync().ConfigureAwait(false);
            all.RemoveAll(existing => existing.Id == session.Id);
            all.Add(session);
            var json = JsonSerializer.Serialize(all, JsonOptions);
            await AtomicTextFileWriter
                .WriteAllTextAsync(StoragePath, json)
                .ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId)
    {
        var all = await LoadAllAsync().ConfigureAwait(false);
        return all
            .Where(session => string.Equals(
                session.HaltungId,
                haltungId,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<List<AiOptimizationSession>> LoadCoreAsync()
    {
        var path = StoragePath;
        try
        {
            if (!File.Exists(path))
                return [];

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AiOptimizationSession>>(json) ?? [];
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"KI-Sanierungssitzungen konnten nicht gelesen werden ({path}): " +
                $"{ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }
}
