using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsQuarantineArchitectureTests
{
    [Fact]
    public void SettingsQuarantineUsesStartupInstanceAndKeepsStaticFacadeThin()
    {
        var root = FindRepositoryRoot();
        var app = Read(root, "src", "AuswertungPro.Next.UI", "App.xaml.cs");
        var appSettings = Read(root, "src", "AuswertungPro.Next.UI", "AppSettings.cs");
        var provider = Read(root, "src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var facade = Read(root, "src", "AuswertungPro.Next.UI", "SettingsQuarantine.cs");

        Assert.Contains("var settingsQuarantine = new SettingsQuarantineStore()", app);
        Assert.Contains("AppSettings.Load(settingsQuarantine)", app);
        Assert.Contains("settingsQuarantine);", app);
        Assert.Contains(
            "internal static AppSettings Load(ISettingsQuarantineStore settingsQuarantine)",
            appSettings);
        Assert.Contains("settingsQuarantine.TryMoveToQuarantine", appSettings);
        Assert.DoesNotContain("SettingsQuarantine.TryMoveToQuarantine", appSettings);
        Assert.Contains(
            "public ISettingsQuarantineStore SettingsQuarantine",
            provider);
        Assert.Contains("SettingsQuarantine = settingsQuarantine", provider);
        Assert.DoesNotContain("File.Move", facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.DoesNotContain("File.Delete", facade);
        Assert.DoesNotContain("Directory.CreateDirectory", facade);
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
