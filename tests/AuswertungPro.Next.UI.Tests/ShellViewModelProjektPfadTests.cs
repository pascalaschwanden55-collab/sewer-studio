using System;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Aufgabe 17 (Kleinigkeiten, Optikanalyse 28.09.2026): <c>ShellViewModel.ProjektPfad</c> ist der
/// ToolTip unter der Wortmarke. <c>Settings.LastProjectPath</c> ist eine programmweite
/// "zuletzt benutzt"-Einstellung — ohne den zusaetzlichen <c>HasPersistedProject</c>-Schutz (derselbe
/// Schutz, den <c>TrySaveProjectCore</c> schon gegen ein Ueberschreiben verwendet) haette ein neues,
/// noch nicht gespeichertes Projekt hier den Pfad des ZULETZT geoeffneten Projekts gezeigt.
/// </summary>
public sealed class ShellViewModelProjektPfadTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public ShellViewModelProjektPfadTests()
    {
        var settings = new AppSettings { EnableRestorePoints = false };
        _services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory);
        _shell = new ShellViewModel(_services, new SystemMonitorService(enableHardwareSensorInit: false));
    }

    [Fact]
    public void Ohne_offenes_Projekt_ist_der_Pfad_leer()
        => Assert.Null(_shell.ProjektPfad);

    [Fact]
    public void Ein_neues_noch_nicht_gespeichertes_Projekt_zeigt_nicht_den_Pfad_des_vorherigen_Projekts()
    {
        // Projekt A wird geoeffnet/gespeichert wie im echten Ablauf (AddRecentProject vor MarkProjectReady).
        _services.Settings.AddRecentProject(@"C:\Projekte\A\Projektdateien\projekt.json");
        _shell.ReplaceProject(new Project { Name = "Projekt A" });
        _shell.MarkProjectReady();
        _shell.HasPersistedProject = true;
        Assert.Equal(@"C:\Projekte\A\Projektdateien\projekt.json", _shell.ProjektPfad);

        // "Neues Projekt": Settings.LastProjectPath zeigt technisch weiterhin auf A, bis das neue
        // Projekt zum ersten Mal gespeichert wird — HasPersistedProject markiert genau das.
        _shell.ReplaceProject(new Project { Name = "Neues Projekt" });
        _shell.ResetProjectReady();
        _shell.HasPersistedProject = false;

        Assert.Null(_shell.ProjektPfad);
    }

    /// <summary>
    /// Deckt genau die Reihenfolge ab, die im echten Speicherweg vorkommt: Ist das Projekt schon
    /// "bereit" (z. B. weil vorher schon ein anderes Projekt offen war) und wird nur
    /// <c>HasPersistedProject</c> zuletzt gesetzt (kein erneutes <c>IsProjectReady</c>-Ereignis, weil
    /// der Wert sich nicht aendert), muss der ToolTip trotzdem nachziehen.
    /// </summary>
    [Fact]
    public void Der_Pfad_zieht_nach_wenn_nur_HasPersistedProject_zuletzt_wechselt()
    {
        _shell.MarkProjectReady();
        Assert.Null(_shell.ProjektPfad);

        _services.Settings.AddRecentProject(@"C:\Projekte\Neu\Projektdateien\projekt.json");
        _shell.HasPersistedProject = true;

        Assert.Equal(@"C:\Projekte\Neu\Projektdateien\projekt.json", _shell.ProjektPfad);
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
    }
}
