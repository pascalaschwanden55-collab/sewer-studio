using System;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 2): «Rückgängig»/«Wiederholen» (Aufgabe 16) sind
/// auch ueber die globale Suche (Strg+K) erreichbar - derselbe Befehl, dieselbe Beschriftung wie
/// im Menue «_Bearbeiten», dieselbe Glyph. Verfuegbarkeit folgt dem echten
/// <see cref="ShellViewModel.RueckgaengigCommand"/>/<see cref="ShellViewModel.WiederholenCommand"/>;
/// die Suche haengt an deren <c>CanExecuteChanged</c>, genau wie an Speichern/Neu/Oeffnen.
/// </summary>
public sealed class GlobaleSucheRueckgaengigWiederholenTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public GlobaleSucheRueckgaengigWiederholenTests()
    {
        _services = new ServiceProvider(new AppSettings { EnableRestorePoints = false }, new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"), _loggerFactory);
        _shell = new ShellViewModel(_services, new SystemMonitorService(enableHardwareSensorInit: false));
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
    }

    private HaltungRecord OeffneHaltungen()
    {
        var projekt = new Project { Name = "Verlauf" };
        var h = new HaltungRecord();
        h.Fields[FieldKeys.HoldingName] = "10001-10002";
        projekt.Data.Add(h);
        _shell.ReplaceProject(projekt);
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Haltungen");
        Assert.IsType<DataPageViewModel>(_shell.CurrentPage);
        return h;
    }

    private void Eingabe(HaltungRecord h, string wert)
    {
        using var _ = _services.DatenaenderungsVerlauf.Erfasse(h, FieldKeys.PipeMaterial);
        h.SetFieldValue(FieldKeys.PipeMaterial, wert, FieldSource.Manual, userEdited: true);
    }

    /// <summary>Ohne Verlaufseintrag erscheint kein Treffer - der echte
    /// <see cref="ShellViewModel.RueckgaengigCommand"/> ist dann nicht ausfuehrbar.</summary>
    [Fact]
    public void Ohne_verlaufseintrag_ist_rueckgaengig_kein_treffer()
    {
        OeffneHaltungen();
        var suche = _shell.GlobaleSuche;

        suche.Text = "rückgängig";

        Assert.DoesNotContain(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
    }

    /// <summary>Mit Verlaufseintrag traegt der Treffer denselben Text wie
    /// <see cref="ShellViewModel.RueckgaengigMenuText"/> ("Rückgängig: Rohrmaterial 10001-10002")
    /// und dieselbe Glyph wie im Menue (&#xE7A7;), und das Ausfuehren nimmt die Eingabe wirklich
    /// zurueck.</summary>
    [Fact]
    public void Rueckgaengig_treffer_nennt_die_beschreibung_und_fuehrt_den_echten_befehl_aus()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        var suche = _shell.GlobaleSuche;

        suche.Text = "rückgängig";
        var treffer = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
        Assert.Equal("Rückgängig: Rohrmaterial 10001-10002", treffer.Text);
        Assert.Equal("", treffer.Glyph);

        suche.Waehle(treffer);

        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.Equal(string.Empty, suche.Text);
    }

    /// <summary>Nach dem Rueckgaengig-Schritt ist «Wiederholen» ueber die Suche erreichbar und
    /// stellt den Wert wieder her.</summary>
    [Fact]
    public void Wiederholen_treffer_stellt_die_eingabe_wieder_her()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        _shell.RueckgaengigCommand.Execute(null);
        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));

        var suche = _shell.GlobaleSuche;
        suche.Text = "wiederholen";
        var treffer = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
        Assert.Equal("Wiederholen: Rohrmaterial 10001-10002", treffer.Text);
        Assert.Equal("", treffer.Glyph);

        suche.Waehle(treffer);

        Assert.Equal("PVC", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    /// <summary>Nachtrag: Traegt die Beschreibung selbst einen Unterstrich (moeglich bei einer
    /// Sammelbearbeitung mit eigenem Beschreibungstext), zeigt die Trefferliste ihn UNVERAENDERT -
    /// anders als <see cref="ShellViewModel.RueckgaengigMenuText"/> fuer das Menue «_Bearbeiten»,
    /// das ihn wegen der WPF-Zugriffstaste verdoppeln muss.</summary>
    [Fact]
    public void Rueckgaengig_treffer_verdoppelt_keinen_unterstrich_in_der_beschreibung()
    {
        var h = OeffneHaltungen();
        using (_services.DatenaenderungsVerlauf.ErfasseMehrere([h], "Sammelbearbeitung: Feld_Name 10001-10002"))
            h.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Manual, userEdited: true);
        var suche = _shell.GlobaleSuche;

        suche.Text = "rückgängig";

        var treffer = Assert.Single(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
        Assert.Equal("Rückgängig: Sammelbearbeitung: Feld_Name 10001-10002", treffer.Text);
        Assert.Contains("Feld__Name", _shell.RueckgaengigMenuText);
    }

    /// <summary>Verschwindet der Verlaufseintrag (z. B. Projektwechsel), waehrend die Trefferliste
    /// bereits offen ist, verschwindet auch der «Rückgängig»-Treffer selbststaendig - dieselbe
    /// Live-Kopplung wie bei «Speichern» und einem Betriebs-Schutz.</summary>
    [Fact]
    public void Ein_rueckgaengig_treffer_verschwindet_sobald_der_verlauf_geleert_wird()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        var suche = _shell.GlobaleSuche;

        suche.Text = "rückgängig";
        Assert.Contains(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);

        _services.DatenaenderungsVerlauf.Leere("Test");

        Assert.DoesNotContain(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl);
    }
}
