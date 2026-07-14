using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsMigrationArchitectureTests
{
    [Fact]
    public void SettingsMigrationUsesStartupInstanceAndKeepsStaticFacadeThin()
    {
        var root = FindRepositoryRoot();
        var app = Read(root, "src", "AuswertungPro.Next.UI", "App.xaml.cs");
        var appSettings = Read(root, "src", "AuswertungPro.Next.UI", "AppSettings.cs");
        var provider = Read(root, "src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var facade = Read(root, "src", "AuswertungPro.Next.UI", "SettingsMigrator.cs");

        Assert.Contains("var settingsMigration = new SettingsMigrationService()", app);
        Assert.Contains("settingsMigration);", app);
        Assert.Contains("ISettingsMigrationService settingsMigration", appSettings);
        Assert.Contains("settingsMigration.MigrateLegacyIfNeeded", appSettings);
        Assert.Contains("migrationResult.Error", appSettings);
        Assert.Contains("TryAppendSettingsLog", appSettings);
        Assert.DoesNotContain("SettingsMigrator.MigrateLegacyIfNeeded", appSettings);
        Assert.Contains("public ISettingsMigrationService SettingsMigration", provider);
        Assert.Contains("SettingsMigration = settingsMigration", provider);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.DoesNotContain("Directory.CreateDirectory", facade);
        Assert.DoesNotContain("catch", facade);
    }

    private static string Read(string root, params string[] segments)
        => File.ReadAllText(Path.Combine([root, .. segments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
