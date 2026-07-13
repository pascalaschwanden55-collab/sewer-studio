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

/// <summary>
/// Regressionstest zum AP-50-Review: Nach der Umstellung von OpenSelectedCommand auf
/// AsyncRelayCommand darf der CanExecute-Refresh nicht mehr ueber '(… as RelayCommand)'
/// laufen (Cast liefert null -> stiller No-Op -> Oeffnen-Button bleibt deaktiviert).
/// </summary>
public sealed class OverviewOpenCommandCanExecuteTests
{
    [Fact]
    public void OpenSelectedCommand_WirdAktiviert_WennProjektAusgewaehlt()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Testprojekt.json");
        File.WriteAllText(projectFile, "{\"Version\":2,\"Name\":\"Testprojekt\"}");

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

        var entry = vm.ProjectEntries.First(e =>
            string.Equals(e.Path, projectFile, StringComparison.OrdinalIgnoreCase));

        // Die Uebersicht waehlt beim Laden bewusst das erste Projekt vor. Fuer den
        // eigentlichen Zustandswechsel hier eine leere Auswahl herstellen.
        vm.SelectedProjectEntry = null;

        // Ausgangslage: nichts ausgewaehlt -> Oeffnen-Button aus.
        Assert.False(vm.OpenSelectedCommand.CanExecute(null));

        var canExecuteChangedGefeuert = false;
        vm.OpenSelectedCommand.CanExecuteChanged += (_, _) => canExecuteChangedGefeuert = true;

        // Auswahl setzen: OnSelectedProjectEntryChanged muss NotifyCanExecuteChanged ausloesen.
        vm.SelectedProjectEntry = entry;

        Assert.True(canExecuteChangedGefeuert,
            "OpenSelectedCommand.CanExecuteChanged muss nach Auswahl feuern, sonst bleibt der Oeffnen-Button deaktiviert");
        Assert.True(vm.OpenSelectedCommand.CanExecute(null));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "OverviewOpenCmdTests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
            catch { /* Best-effort cleanup. */ }
        }
    }
}
