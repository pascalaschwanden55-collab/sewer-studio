using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsFileStoreDependencyTests
{
    [Fact]
    public void SaveImmediate_verwendet_den_injizierten_Dateispeicher()
    {
        var store = new RecordingSettingsFileStore();
        var settings = new AppSettings
        {
            LastProjectPath = @"D:\Projekte\Altdorf",
            EnableRestorePoints = false
        };
        settings.UseSettingsFileStore(store);

        settings.SaveImmediate();

        Assert.Equal(1, store.Calls);
        Assert.Contains("Altdorf", store.Json);
        Assert.EndsWith("settings.json", store.SettingsPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(store.EnableRestorePoints);
    }

    [Fact]
    public void FlushPendingSave_verwendet_den_beim_Speichern_injizierten_Dateispeicher()
    {
        var store = new RecordingSettingsFileStore();
        var settings = new AppSettings { LastProjectPath = @"D:\Projekte\Erstfeld" };
        settings.UseSettingsFileStore(store);

        settings.Save();
        AppSettings.FlushPendingSave();

        Assert.Equal(1, store.Calls);
        Assert.Contains("Erstfeld", store.Json);
    }

    private sealed class RecordingSettingsFileStore : ISettingsFileStore
    {
        public int Calls { get; private set; }
        public string Json { get; private set; } = string.Empty;
        public string SettingsPath { get; private set; } = string.Empty;
        public bool EnableRestorePoints { get; private set; }

        public void Persist(
            string json,
            string settingsPath,
            string appDataDirectory,
            bool enableRestorePoints)
        {
            Calls++;
            Json = json;
            SettingsPath = settingsPath;
            EnableRestorePoints = enableRestorePoints;
        }
    }
}
