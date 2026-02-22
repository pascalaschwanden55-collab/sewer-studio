using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

public static class AiOptimizationSessionStore
{
    private static readonly string FilePath =
        Path.Combine(AppSettings.AppDataDir, "ai_sanierung_sessions.json");

    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public static async Task SaveAsync(AiOptimizationSession session)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var all = await LoadCoreAsync().ConfigureAwait(false);
            all.RemoveAll(s => s.Id == session.Id);
            all.Add(session);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(all, _jsonOpts);
            await File.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId)
    {
        var all = await LoadAllAsync().ConfigureAwait(false);
        return all
            .Where(s => string.Equals(s.HaltungId, haltungId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static async Task<List<AiOptimizationSession>> LoadCoreAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
                return [];
            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AiOptimizationSession>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
