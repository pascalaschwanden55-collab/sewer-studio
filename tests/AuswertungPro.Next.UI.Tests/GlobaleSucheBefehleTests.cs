using System;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 14: Die globale Suche (Strg+K) findet neben Haltungen,
/// Schaechten und Strassen auch Befehle und fuehrt sie ueber die echten ShellViewModel-Befehle
/// aus — kein Fake-Dialogdienst noetig, weil keiner der hier geprueften Befehle einen Dialog
/// oeffnet.
/// </summary>
public sealed class GlobaleSucheBefehleTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public GlobaleSucheBefehleTests()
    {
        var settings = new AppSettings { EnableRestorePoints = false };
        _services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory);
        _shell = new ShellViewModel(_services, new SystemMonitorService(enableHardwareSensorInit: false));
    }

    private static Project Projekt(string name) => new() { Name = name };

    /// <summary>Ein gefundener Befehl ruft wirklich den ShellViewModel-Befehl auf — hier
    /// "Fokusmodus" (<see cref="ShellViewModel.ToggleFocusModeCommand"/>), sichtbar am
    /// tatsaechlich umgeschalteten <see cref="ShellViewModel.IsFocusMode"/>.</summary>
    [Fact]
    public void Ausfuehren_eines_Befehlstreffers_ruft_den_echten_ShellViewModel_Befehl_auf()
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;
        Assert.False(_shell.IsFocusMode);

        suche.Text = "fokusmodus";
        var treffer = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
        Assert.Equal("Fokusmodus", treffer.Text);

        suche.Waehle(treffer);

        Assert.True(_shell.IsFocusMode);
        Assert.Equal(string.Empty, suche.Text);
        Assert.False(suche.ListeOffen);
    }

    /// <summary>Eine «Gehe zu: …»-Zeile navigiert wirklich ueber <see cref="ShellViewModel.NavigateTo"/>.</summary>
    [Fact]
    public void Gehe_zu_Treffer_navigiert_zur_echten_Seite()
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = "gehe zu: einstellungen";
        var treffer = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);

        suche.Waehle(treffer);

        Assert.Equal("Einstellungen", _shell.SelectedNavItem?.Title);
    }

    /// <summary>Ohne offenes Projekt (Launcher, vor jedem <c>ReplaceProject</c>) ist «Speichern»
    /// nicht ausfuehrbar (<see cref="ShellViewModel.SaveCommand"/> verlangt den Arbeitsbereich)
    /// und darf deshalb gar nicht erst in der Trefferliste erscheinen.</summary>
    [Fact]
    public void Speichern_ist_ohne_offenes_Projekt_kein_Treffer()
    {
        var suche = _shell.GlobaleSuche;

        suche.Text = "speichern";

        Assert.DoesNotContain(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl && t.Text == "Speichern");
    }

    /// <summary>Sobald ein Projekt im Arbeitsbereich offen ist, erscheint «Speichern» und fuehrt
    /// wirklich <see cref="ShellViewModel.TrySaveProject"/> respektive den Speicherweg aus
    /// (hier nur die Sichtbarkeit/CanExecute-Kopplung geprueft, kein Datei-I/O).</summary>
    [Fact]
    public void Speichern_erscheint_sobald_der_Arbeitsbereich_offen_ist()
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = "speichern";

        Assert.Contains(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl && t.Text == "Speichern");
    }

    /// <summary>Die Trefferliste traegt einen Gruppenkopf «Befehle», sobald mindestens ein
    /// Befehl matcht, aber keinen, solange die Suche ausschliesslich Daten trifft.</summary>
    [Fact]
    public void Ein_Gruppenkopf_Befehle_erscheint_nur_wenn_auch_ein_Befehl_trifft()
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "10001-10002", FieldSource.Manual, false);
        var projekt = Projekt("Projekt A");
        projekt.Data.Add(haltung);
        _shell.ReplaceProject(projekt);
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        // Reine Datensuche ohne Befehlstreffer: kein Gruppenkopf.
        suche.Text = "10001-10002";
        Assert.DoesNotContain(suche.Treffer, t => t.Art == GlobaleSucheArt.Gruppenkopf);

        // Suche mit Befehlstreffer: genau ein Gruppenkopf "Befehle".
        suche.Text = "einstellungen";
        var kopf = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Gruppenkopf);
        Assert.Equal("Befehle", kopf.Text);
    }

    /// <summary>Pfeiltasten ueberspringen den Gruppenkopf: Markieren landet nie auf ihm, und
    /// Enter (<see cref="GlobaleSucheViewModel.WaehleErstenOderMarkierten"/>) waehlt trotzdem
    /// einen echten Treffer statt nichts zu tun.</summary>
    [Fact]
    public void Pfeiltasten_ueberspringen_den_Gruppenkopf()
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = "einstellungen";
        Assert.Contains(suche.Treffer, t => t.Art == GlobaleSucheArt.Gruppenkopf);

        suche.MarkiereNaechsten();

        Assert.InRange(suche.MarkiertIndex, 0, suche.Treffer.Count - 1);
        Assert.NotEqual(GlobaleSucheArt.Gruppenkopf, suche.Treffer[suche.MarkiertIndex].Art);
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
    }
}
