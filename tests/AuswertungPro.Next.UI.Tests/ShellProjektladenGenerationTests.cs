using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Auditbefund 12 (18.09.2026): Waehrend eines langsamen Projektladens konnte der Benutzer
/// ein neues Projekt beginnen. Der alte Ladevorgang ersetzte den frischen Entwurf danach
/// ohne erneute Pruefung — die begonnene Arbeit war weg.
///
/// Die Uebernahme prueft deshalb, ob das Projekt seit dem Start des Ladens noch dasselbe
/// ist. Hat sich der Stand geaendert, wird das verspaetete Ergebnis verworfen.
/// </summary>
public sealed class ShellProjektladenGenerationTests
{
    [Fact]
    public void Ein_verspaetetes_Ladeergebnis_ersetzt_den_neuen_Entwurf_nicht()
    {
        using var temp = new TempOrdner();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));

        var altesProjekt = new Project { Name = "Alt" };
        shell.ReplaceProject(altesProjekt);

        // Der Ladevorgang merkt sich den Stand beim Start.
        var generationBeimStart = shell.ProjectGeneration;

        // Waehrend das Laden laeuft, beginnt der Benutzer ein neues Projekt.
        var neuerEntwurf = new Project { Name = "Neuer Entwurf" };
        shell.ReplaceProject(neuerEntwurf);

        // Jetzt kommt das alte Ladeergebnis zurueck.
        var spaetesErgebnis = Result<Project>.Success(new Project { Name = "Spaet geladen" });
        var uebernommen = shell.ApplyLoadOutcome(
            Path.Combine(temp.Pfad, "projekt.json"),
            spaetesErgebnis,
            recovery: null,
            importRecovery: null,
            startGeneration: generationBeimStart);

        Assert.False(uebernommen, "Ein verspaetetes Ergebnis darf nicht uebernommen werden.");
        Assert.Same(neuerEntwurf, shell.Project);
    }

    [Fact]
    public void Ein_unveraenderter_Stand_wird_normal_uebernommen()
    {
        using var temp = new TempOrdner();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));

        var generationBeimStart = shell.ProjectGeneration;
        var geladen = new Project { Name = "Geladen" };

        var uebernommen = shell.ApplyLoadOutcome(
            Path.Combine(temp.Pfad, "projekt.json"),
            Result<Project>.Success(geladen),
            recovery: null,
            importRecovery: null,
            startGeneration: generationBeimStart);

        Assert.True(uebernommen);
        Assert.Same(geladen, shell.Project);
    }

    private sealed class TempOrdner : IDisposable
    {
        public TempOrdner()
        {
            Pfad = Path.Combine(Path.GetTempPath(), "shell-gen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Pfad);
        }

        public string Pfad { get; }

        public void Dispose()
        {
            try { Directory.Delete(Pfad, recursive: true); } catch { }
        }
    }
}
