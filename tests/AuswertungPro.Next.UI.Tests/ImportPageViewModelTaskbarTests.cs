using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration): der Ein-Knopf-Import spiegelt
/// seinen Fortschritt am Programmsymbol in der Taskleiste. Prueft die Verdrahtung
/// (ImportPageViewModel -&gt; ITaskbarFortschritt) mit einem Fake statt einem echten Fenster.
/// </summary>
public sealed class ImportPageViewModelTaskbarTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public void Start_zeigt_unbestimmten_Fortschritt_bestimmter_Fortschritt_ersetzt_ihn_und_Ende_setzt_zurueck()
    {
        var taskbar = new FakeTaskbarFortschritt();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Taskbar = taskbar
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        Assert.Empty(taskbar.Aufrufe);

        // OnIsImportInProgressChanged setzt ImportIsIndeterminate=true selbst (Anfangszustand
        // eines Laufs, dessen Groesse noch nicht bekannt ist).
        viewModel.IsImportInProgress = true;
        Assert.Equal("unbestimmt", taskbar.Aufrufe[^1]);

        // Ein bestimmter Fortschritt loest den unbestimmten Zustand ab.
        viewModel.ImportIsIndeterminate = false;
        viewModel.ImportProgressPercent = 42;
        Assert.Equal("fortschritt:0.42", taskbar.Aufrufe[^1]);

        // Fertig -> Taskleiste wird zurueckgesetzt, egal was zuletzt angezeigt war.
        viewModel.IsImportInProgress = false;
        Assert.Equal("beenden", taskbar.Aufrufe[^1]);
    }

    [Fact]
    public void Fortschrittsaenderungen_ausserhalb_eines_Laufs_setzen_die_Taskleiste_nicht_in_Bewegung()
    {
        var taskbar = new FakeTaskbarFortschritt();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Taskbar = taskbar
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        // IsImportInProgress bleibt false - ein spaeter eintreffender Prozentwert (z. B. Rest
        // eines vorherigen Laufs) darf die Taskleiste nicht wieder aufleben lassen.
        viewModel.ImportProgressPercent = 77;

        Assert.All(taskbar.Aufrufe, aufruf => Assert.Equal("beenden", aufruf));
    }
}
