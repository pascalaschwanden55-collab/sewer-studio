using System;
using System.Linq;
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

    /// <summary>Fix-Runde 1, Befund 2: Jeder der sieben im Auftrag genannten Hauptbefehle traegt
    /// dasselbe Symbol wie sein Gegenstueck im Menue Datei/Hilfe (<c>MainWindow.xaml</c>), plus
    /// Fokusmodus mit einem eigens gewaehlten, sinnvollen Symbol. Kein Treffer bleibt ohne
    /// Glyph — das leere Symbolfeld im Popup wuerde sonst eine Luecke zeigen.</summary>
    [Theory]
    [InlineData("Neues Projekt")]
    [InlineData("Projekt öffnen")]
    [InlineData("Speichern")]
    [InlineData("Speichern unter")]
    [InlineData("Handbuch")]
    [InlineData("Tastenkürzel")]
    [InlineData("Über SewerStudio")]
    [InlineData("Fokusmodus")]
    public void Jeder_hauptbefehl_hat_ein_sichtbares_glyph(string anzeigename)
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = anzeigename;

        var treffer = Assert.Single(
            suche.Treffer,
            t => t.Art == GlobaleSucheArt.Befehl && t.Text == anzeigename);
        Assert.False(string.IsNullOrEmpty(treffer.Glyph));
    }

    /// <summary>Fix-Runde 1, Befund 1: Ein Betriebs-Schutz (Import/Export laeuft) kann aktiv
    /// werden, WAEHREND die Trefferliste bereits offen ist. Der bereits erfasste Treffer darf
    /// dann nichts mehr ausloesen — die Ausfuehrung prueft <c>CanExecute</c> unmittelbar vor dem
    /// echten Aufruf ein zweites Mal, statt sich auf den beim Bauen der Liste erfassten Stand zu
    /// verlassen. Nachgewiesen ueber den echten Speicherweg: <see cref="ShellViewModel.SaveCommand"/>
    /// wuerde ohne bestehende Projektdatei einen Speichern-Dialog oeffnen — dieser darf gar nicht
    /// erst aufgerufen werden.</summary>
    [Fact]
    public void Speichern_fuehrt_nichts_mehr_aus_wenn_waehrend_der_anzeige_ein_betriebs_schutz_aktiv_wird()
    {
        var dialogs = new AufzeichnenderDialogService();
        _services.Dialogs = dialogs;
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = "speichern";
        var treffer = suche.Treffer.Single(t => t.Art == GlobaleSucheArt.Befehl && t.Text == "Speichern");

        var schutz = new SperrenderBetriebsSchutz();
        _shell.RegisterShellOperationGuard(schutz);
        try
        {
            // Auch ein VOR der Sperre erfasster Treffer (Race zwischen Anzeige und Klick) darf
            // nichts mehr ausloesen.
            suche.Waehle(treffer);

            Assert.Equal(0, dialogs.SaveFileCalls);
        }
        finally
        {
            _shell.UnregisterShellOperationGuard(schutz);
        }
    }

    /// <summary>Fix-Runde 1, Befund 1 (zweiter Teil): Wird ein Betriebs-Schutz aktiv, WAEHREND die
    /// Trefferliste bereits offen ist (ohne dass neu getippt wird), verschwindet der jetzt
    /// gesperrte Befehlstreffer selbststaendig aus der sichtbaren Liste — kein stehen bleibender,
    /// scheinbar noch anklickbarer Eintrag.</summary>
    [Fact]
    public void Ein_treffer_verschwindet_aus_der_offenen_liste_sobald_ein_betriebs_schutz_aktiv_wird()
    {
        _shell.ReplaceProject(Projekt("Projekt A"));
        _shell.MarkProjectReady();
        _shell.EnterWorkspaceOn("Uebersicht");
        var suche = _shell.GlobaleSuche;

        suche.Text = "speichern";
        Assert.Contains(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl && t.Text == "Speichern");

        var schutz = new SperrenderBetriebsSchutz();
        _shell.RegisterShellOperationGuard(schutz);
        try
        {
            Assert.DoesNotContain(suche.Treffer, t => t.Art == GlobaleSucheArt.Befehl && t.Text == "Speichern");
        }
        finally
        {
            _shell.UnregisterShellOperationGuard(schutz);
        }
    }

    /// <summary>Sperrt Speichern/Speichern unter/Neues Projekt/Projekt oeffnen wie ein laufender
    /// Import/Export es tut (siehe <see cref="AlwaysSavingGuard"/> in
    /// <c>ShellProjectOperationReservationTests</c> fuer die Gegenprobe).</summary>
    private sealed class SperrenderBetriebsSchutz : IShellOperationGuard
    {
        public bool CanSaveProjectFromShell => false;
        public string ProjectSaveBlockedMessage => "Ein Vorgang läuft.";
        public bool AllowsInternalProjectSave => false;
        public bool CanLeaveShellContext => false;
        public string LeaveBlockedMessage => "Ein Vorgang läuft.";
        public event EventHandler? OperationAvailabilityChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class AufzeichnenderDialogService : IDialogService
    {
        public int SaveFileCalls { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls++;
            return null;
        }

        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();

        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => false;

        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => false;

        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Cancel;
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
    }
}
