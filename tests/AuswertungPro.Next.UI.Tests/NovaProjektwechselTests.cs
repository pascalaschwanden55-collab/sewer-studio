using System;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// R1, R2 und R4 des Gesamtaudits vom 08.09.2026: Beim Wechsel des Projekts darf nichts
/// aus dem vorherigen Projekt stehen bleiben — weder ein Suchtreffer, noch die Zahlen der
/// Uebersicht, noch ein KI-Vorabdurchlauf.
/// </summary>
public sealed class NovaProjektwechselTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public NovaProjektwechselTests()
    {
        var settings = new AppSettings { EnableRestorePoints = false };
        _services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory);
        _shell = new ShellViewModel(_services, new SystemMonitorService(enableHardwareSensorInit: false));
    }

    private static Project Projekt(string name, params string[] haltungen)
    {
        var p = new Project { Name = name };
        foreach (var h in haltungen)
            p.Data.Add(Haltung(h));
        return p;
    }

    private static HaltungRecord Haltung(string name)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        return r;
    }

    /// <summary>R1: In A suchen, B oeffnen — die Trefferliste von A muss verschwinden.</summary>
    [Fact]
    public void Nach_Projektwechsel_bleibt_kein_Suchtreffer_des_alten_Projekts_stehen()
    {
        _shell.ReplaceProject(Projekt("Projekt A", "10001-10002"));
        _shell.MarkProjectReady();
        var suche = _shell.GlobaleSuche;

        suche.Text = "10001-10002";
        Assert.NotEmpty(suche.Treffer);

        _shell.ReplaceProject(Projekt("Projekt B", "20001-20002"));

        Assert.Empty(suche.Treffer);
        Assert.False(suche.ListeOffen);
    }

    /// <summary>R1: Nach dem Wechsel findet dieselbe Eingabe nur noch Objekte des neuen Projekts.</summary>
    [Fact]
    public void Nach_Projektwechsel_sucht_dieselbe_Eingabe_im_neuen_Projekt()
    {
        _shell.ReplaceProject(Projekt("Projekt A", "10001-10002"));
        _shell.MarkProjectReady();
        var suche = _shell.GlobaleSuche;
        suche.Text = "1000";
        Assert.NotEmpty(suche.Treffer);

        _shell.ReplaceProject(Projekt("Projekt B", "20001-20002"));
        suche.Text = "1000";

        Assert.Empty(suche.Treffer);
    }

    /// <summary>
    /// R1, zweite Sicherung: Selbst wenn ein Treffer eines fremden Projekts noch in der Liste
    /// stuende, darf er nichts oeffnen. Sonst zeigt die Haltungsseite einen Datensatz, den das
    /// offene Projekt gar nicht enthaelt.
    /// </summary>
    [Fact]
    public void Ein_Treffer_aus_einem_fremden_Projekt_oeffnet_nichts()
    {
        var fremd = Haltung("10001-10002");
        _shell.ReplaceProject(Projekt("Projekt B", "20001-20002"));
        _shell.MarkProjectReady();
        var suche = _shell.GlobaleSuche;
        suche.Treffer.Add(new GlobaleSucheTreffer(GlobaleSucheArt.Haltung, "Haltung 10001-10002", fremd));

        var seiteVorher = _shell.CurrentPage;

        suche.Waehle(suche.Treffer[0]);

        Assert.Same(seiteVorher, _shell.CurrentPage);
        Assert.Empty(suche.Treffer);
    }

    /// <summary>R2: Von B nach C wechseln — Titel und Kennzahlen muessen C zeigen.</summary>
    [Fact]
    public void Projektuebersicht_zeigt_nach_Projektwechsel_das_neue_Projekt()
    {
        _shell.ReplaceProject(Projekt("Projekt B", "10001-10002"));
        _shell.MarkProjectReady();
        using var vm = new ProjektUebersichtPageViewModel(_shell, _services);
        Assert.Equal("Projekt B", vm.HeroTitel);
        Assert.Equal(1, vm.Kennzahlen!.Haltungen);

        _shell.ReplaceProject(Projekt("Projekt C"));

        Assert.Equal("Projekt C", vm.HeroTitel);
        Assert.Equal(0, vm.Kennzahlen!.Haltungen);
    }

    /// <summary>
    /// R2: Nach dem Wechsel muss die NEUE Haltungsliste beobachtet werden. Ein Fix, der nur
    /// einmal neu rechnet, das Abo aber an der alten Liste laesst, faellt hier durch.
    /// </summary>
    [Fact]
    public void Projektuebersicht_beobachtet_nach_dem_Wechsel_die_neue_Haltungsliste()
    {
        _shell.ReplaceProject(Projekt("Projekt B", "10001-10002", "10002-10003"));
        _shell.MarkProjectReady();
        using var vm = new ProjektUebersichtPageViewModel(_shell, _services);

        var c = Projekt("Projekt C");
        _shell.ReplaceProject(c);
        c.Data.Add(Haltung("30001-30002"));

        Assert.Equal(1, vm.Kennzahlen!.Haltungen);
    }

    /// <summary>R4: Ein Durchlauf aus Projekt A darf in der Uebersicht von B nicht erscheinen.</summary>
    [Fact]
    public void Projektuebersicht_zeigt_keinen_KI_Lauf_eines_anderen_Projekts()
    {
        var a = Projekt("Projekt A", "10001-10002");
        _services.CodingSuggestionRegistry.Merke(a.Id, "nur-in-Projekt-A", CodingSuggestionSet.Leer("x"));

        _shell.ReplaceProject(Projekt("Projekt B"));
        _shell.MarkProjectReady();
        using var vm = new ProjektUebersichtPageViewModel(_shell, _services);

        Assert.Empty(vm.KiLaeufe);
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
    }
}
