using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.Infrastructure.Tests.Settings;

public sealed class SettingsMigrationServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"SettingsMigrationServiceTests_{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_CopiesLegacySettingsThroughContract()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy", "settings.json");
        var appDataDirectory = Path.Combine(_tempDirectory, "current");
        var settingsPath = Path.Combine(appDataDirectory, "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "{\"language\":\"de\"}");
        ISettingsMigrationService service = new SettingsMigrationService();

        var result = service.MigrateLegacyIfNeeded(
            settingsPath,
            legacyPath,
            appDataDirectory);

        Assert.True(result.Migrated);
        Assert.Null(result.Error);
        Assert.Equal(File.ReadAllText(legacyPath), File.ReadAllText(settingsPath));
    }

    [Fact]
    public void InstanceService_DoesNotOverwriteCurrentSettings()
    {
        Directory.CreateDirectory(_tempDirectory);
        var legacyPath = Path.Combine(_tempDirectory, "legacy.json");
        var settingsPath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(legacyPath, "{\"source\":\"legacy\"}");
        File.WriteAllText(settingsPath, "{\"source\":\"current\"}");
        ISettingsMigrationService service = new SettingsMigrationService();

        var result = service.MigrateLegacyIfNeeded(
            settingsPath,
            legacyPath,
            _tempDirectory);

        Assert.False(result.Migrated);
        Assert.Null(result.Error);
        Assert.Contains("current", File.ReadAllText(settingsPath));
    }

    [Fact]
    public void InstanceService_ReturnsCopyFailureForLogging()
    {
        Directory.CreateDirectory(_tempDirectory);
        var legacyPath = Path.Combine(_tempDirectory, "legacy.json");
        var blockedDirectory = Path.Combine(_tempDirectory, "blocked");
        File.WriteAllText(legacyPath, "{}");
        File.WriteAllText(blockedDirectory, "kein Ordner");
        ISettingsMigrationService service = new SettingsMigrationService();

        var result = service.MigrateLegacyIfNeeded(
            Path.Combine(blockedDirectory, "settings.json"),
            legacyPath,
            blockedDirectory);

        Assert.False(result.Migrated);
        Assert.NotNull(result.Error);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
