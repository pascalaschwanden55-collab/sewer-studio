using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterStore
{
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
        catch (JsonException)
        {
            // Corrupt JSON → backup and start fresh
            try
            {
                var badPath = StoreFilePath + ".bad_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                File.Move(StoreFilePath, badPath, overwrite: true);
            }
            catch
            {
                // ignore
            }

            return new TrainingCenterState();
        }
        catch
        {
            return new TrainingCenterState();
        }
    }

    public async Task SaveAsync(TrainingCenterState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoreFilePath)!);

        state.UpdatedUtc = DateTime.UtcNow;

        await using var fs = File.Create(StoreFilePath);
        await JsonSerializer.SerializeAsync(fs, state, JsonOptions);
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AuswertungPro");
        return Path.Combine(dir, "training_center.json");
    }
}
