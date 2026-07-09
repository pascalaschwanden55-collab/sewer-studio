using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewProjectEntryTests
{
    [Theory]
    [InlineData(12, 5, false, "12 Haltungen · 5 Schaechte")]
    [InlineData(12, 0, false, "12 Haltungen")]
    [InlineData(0, 5, false, "5 Schaechte")]
    [InlineData(0, 0, false, "Leer")]
    [InlineData(0, 0, true, "Datei fehlerhaft")]
    public void StatsText_beruecksichtigt_haltungen_schaechte_und_defekte_dateien(
        int haltungCount,
        int schachtCount,
        bool isCorrupt,
        string expected)
    {
        var entry = new ProjectOverviewEntry
        {
            RecordCount = haltungCount,
            SchachtCount = schachtCount,
            IsCorrupt = isCorrupt
        };

        Assert.Equal(expected, entry.StatsText);
    }

    [Fact]
    public void Projektliste_zeigt_defekte_projektdatei_statt_sie_zu_verstecken()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Kaputt.json");
        File.WriteAllText(projectFile, "{");

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            LastProjectPath = projectFile,
            ProjectsRootDirectory = temp.Path
        };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);

        var entry = vm.ProjectEntries.Single(e =>
            string.Equals(e.Path, projectFile, StringComparison.OrdinalIgnoreCase));

        Assert.True(entry.IsCorrupt);
        Assert.Equal("Kaputt", entry.Name);
        Assert.Equal("Datei fehlerhaft", entry.StatsText);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "OverviewProjectEntryTests_" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
