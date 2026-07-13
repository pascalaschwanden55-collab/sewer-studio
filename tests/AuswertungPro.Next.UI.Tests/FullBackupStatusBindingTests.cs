using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupStatusBindingTests
{
    [Fact]
    public void Main_status_bar_shows_shared_backup_progress()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "MainWindow.xaml"));

        Assert.Contains("PC-Ausfallschutz", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.IsRunning", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.Percent", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.StatusText", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_page_uses_the_same_shared_backup_progress()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SettingsPage.xaml"));

        Assert.Contains("FullBackupOperation.IsRunning", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.Percent", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.CurrentFile", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.StatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("FullBackupOperation.LastBackupInfo", xaml, StringComparison.Ordinal);
    }
}
