using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optik Aufgabe 16: Rueckgaengig/Wiederholen ueber die echte Shell (Menue, Strg+Z) und die echten
/// Seiten-ViewModels; dazu die Verdrahtung der Eingabewege, des Menues und der Tasten.
/// </summary>
public sealed class DatenVerlaufShellTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public DatenVerlaufShellTests()
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

    [Fact]
    public void Menue_Rueckgaengig_nimmt_die_Eingabe_zurueck_und_markiert_das_Projekt_zum_Speichern()
    {
        var h = OeffneHaltungen();
        Assert.False(_shell.RueckgaengigCommand.CanExecute(null));
        Assert.Equal("Rückgängig", _shell.RueckgaengigMenuText);

        Eingabe(h, "PVC");
        _shell.Project.Dirty = false;

        Assert.True(_shell.RueckgaengigCommand.CanExecute(null));
        Assert.True(_shell.RueckgaengigTasteCommand.CanExecute(null)); // kein Textfeld im Fokus
        Assert.Equal("Rückgängig: Rohrmaterial 10001-10002", _shell.RueckgaengigMenuText);

        _shell.RueckgaengigCommand.Execute(null);

        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.True(_shell.Project.Dirty); // der vorhandene Autosave-Weg ist angestossen
        Assert.Equal("Wiederholen: Rohrmaterial 10001-10002", _shell.WiederholenMenuText);

        _shell.WiederholenTasteCommand.Execute(null);
        Assert.Equal("PVC", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    /// <summary>
    /// Schlusswelle (Item 4): Scheitert das Anwenden mitten im Schritt UND der anschliessende
    /// Rueckbau ebenfalls (<c>DatenaenderungsErgebnis.Teilweise=true</c>, "nicht vollständig"),
    /// bleiben an der Haltung trotzdem tatsaechlich geaenderte Feldwerte stehen. Die Seite muss das
    /// Projekt dann TROTZDEM als geaendert markieren und die Anzeige nachziehen — vorher hing das
    /// allein an <c>Angewendet</c> und dieser Fall wurde stillschweigend uebergangen.
    /// </summary>
    [Fact]
    public void Nicht_vollstaendiger_Rueckbau_markiert_das_Projekt_trotzdem_als_geaendert()
    {
        var h = OeffneHaltungen();
        var seite = Assert.IsType<DataPageViewModel>(_shell.CurrentPage);
        using (_services.DatenaenderungsVerlauf.Erfasse(h))
        {
            h.SetFieldValue("DN_mm", "300", FieldSource.Manual, true);
            h.SetFieldValue("Lichte_Breite_mm", "300", FieldSource.Manual, true);
        }
        h.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Fields[Lichte_Breite_mm]")
                throw new InvalidOperationException("Anzeige gestört");
        };
        _shell.Project.Dirty = false;
        var felderErgaenztAufgerufen = false;
        seite.FelderExternErgaenzt += () => felderErgaenztAufgerufen = true;

        _shell.RueckgaengigCommand.Execute(null);

        Assert.True(_shell.Project.Dirty);
        Assert.True(felderErgaenztAufgerufen);
        Assert.False(_shell.RueckgaengigCommand.CanExecute(null)); // Verlauf ist geleert (GrundFehler)
    }

    [Fact]
    public void Projektwechsel_leert_den_Verlauf()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        Assert.True(_shell.RueckgaengigCommand.CanExecute(null));

        _shell.ReplaceProject(new Project { Name = "Anderes" });

        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        Assert.False(_shell.RueckgaengigCommand.CanExecute(null));
    }

    [Fact]
    public void Ein_laufender_Projektvorgang_leert_und_sperrt_den_Verlauf()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        var guard = new SchaltbarerGuard();
        _shell.RegisterShellOperationGuard(guard);
        Assert.True(_shell.RueckgaengigCommand.CanExecute(null));

        guard.Laeuft = true;

        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        Eingabe(h, "Beton");
        Assert.False(_shell.RueckgaengigCommand.CanExecute(null)); // gesperrt, solange der Vorgang laeuft
        _shell.UnregisterShellOperationGuard(guard);
    }

    [Fact]
    public void Auf_anderen_Seiten_ist_Rueckgaengig_nicht_ausfuehrbar()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        _shell.NavigateTo("Einstellungen");

        Assert.False(_shell.RueckgaengigCommand.CanExecute(null));
        Assert.Equal("Rückgängig", _shell.RueckgaengigMenuText);
    }

    [Fact]
    public void Schachtseite_nimmt_nur_Schachtaenderungen_zurueck()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        var s = new SchachtRecord();
        s.Fields["Schachtnummer"] = "80409";
        _services.DatenaenderungsVerlauf.Binde(null);
        _shell.Project.SchaechteData.Add(s);
        _services.DatenaenderungsVerlauf.Binde(_shell.Project);
        Eingabe(h, "Beton");
        using (_services.DatenaenderungsVerlauf.Erfasse(s, "Material"))
            s.SetFieldValue("Material", "Beton", FieldSource.Manual, true);

        _shell.NavigateTo("Schaechte");
        Assert.IsType<SchaechtePageViewModel>(_shell.CurrentPage);
        Assert.Equal("Rückgängig: Material 80409", _shell.RueckgaengigMenuText);

        _shell.RueckgaengigCommand.Execute(null);

        Assert.Equal("", s.GetFieldValue("Material"));
        Assert.Equal("Beton", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Menuetext_zeigt_Unterstriche_und_Textfelder_behalten_ihr_eigenes_Rueckgaengig()
    {
        Assert.Equal("Rückgängig", DatenVerlaufTasten.MenuText("Rückgängig", null));
        Assert.Equal("Rückgängig: PDF__Path 1-2", DatenVerlaufTasten.MenuText("Rückgängig", "PDF_Path 1-2"));
        Assert.False(DatenVerlaufTasten.IstTexteingabe(null));
        Assert.False(DatenVerlaufTasten.IstTexteingabe("Text"));

        bool? textfeld = null, knopf = null;
        var faden = new Thread(() =>
        {
            textfeld = DatenVerlaufTasten.IstTexteingabe(new TextBox());
            knopf = DatenVerlaufTasten.IstTexteingabe(new Button());
        });
        faden.SetApartmentState(ApartmentState.STA);
        faden.Start();
        faden.Join();
        Assert.True(textfeld);
        Assert.False(knopf);
    }

    [Fact]
    public void Eingabewege_beider_Seiten_laufen_durch_den_Verlauf()
    {
        var seiten = Path.Combine(TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI", "Views", "Pages");
        var dataXaml = File.ReadAllText(Path.Combine(seiten, "DataPage.xaml"));
        Assert.Contains("CellEditEnding=\"Grid_CellEditEndingMitVerlauf\"", dataXaml);
        Assert.Contains("PreparingCellForEdit=\"Grid_PreparingCellForEditMitVerlauf\"", dataXaml);
        var dataCode = File.ReadAllText(Path.Combine(seiten, "DataPage.xaml.cs"));
        Assert.Contains("CommitHaltungDetailFieldMitVerlauf,", dataCode);
        Assert.Contains("ComboBox_LostKeyboardFocusMitVerlauf,", dataCode);
        Assert.Contains("ComboBox_SelectionChangedMitVerlauf);", dataCode);
        Assert.Contains("ClearColumnMitVerlauf(result.FieldName!", dataCode);

        var schachtXaml = File.ReadAllText(Path.Combine(seiten, "SchaechtePage.xaml"));
        Assert.Contains("BeginningEdit=\"Grid_BeginningEditMitVerlauf\"", schachtXaml);
        Assert.Contains("CellEditEnding=\"Grid_CellEditEndingMitVerlauf\"", schachtXaml);
        var schachtCode = File.ReadAllText(Path.Combine(seiten, "SchaechtePage.xaml.cs"));
        Assert.Contains("CommitSchachtDetailMitVerlauf,", schachtCode);
        Assert.Contains("lostKeyboardFocus: ComboBox_LostKeyboardFocusMitVerlauf,", schachtCode);
        Assert.Contains("selectionChanged: ComboBox_SelectionChangedMitVerlauf,", schachtCode);
        Assert.Contains("ClearColumnMitVerlauf(feld,", schachtCode);

        // Die Objektakte bekommt den Verlauf auf beiden Seiten mit.
        var vms = Path.Combine(TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI", "ViewModels", "Pages");
        foreach (var datei in new[] { "DataPageViewModel.cs", "SchaechtePageViewModel.cs" })
            Assert.Equal(2, File.ReadAllText(Path.Combine(vms, datei)).Split("services.DatenaenderungsVerlauf);").Length - 1);
    }

    [Fact]
    public void Menue_Bearbeiten_und_Tasten_stehen_im_Hauptfenster()
    {
        var pfad = Path.Combine(TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI", "MainWindow.xaml");
        var doc = XDocument.Load(pfad);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var menues = doc.Descendants(wpf + "Menu").First().Elements(wpf + "MenuItem")
            .Select(m => (string?)m.Attribute("Header")).ToList();
        Assert.Equal(menues.IndexOf("_Datei") + 1, menues.IndexOf("_Bearbeiten"));
        Assert.Equal(menues.IndexOf("_Bearbeiten") + 1, menues.IndexOf("_Werkzeuge"));

        var bearbeiten = doc.Descendants(wpf + "MenuItem").Single(m => (string?)m.Attribute("Header") == "_Bearbeiten");
        var eintraege = bearbeiten.Elements(wpf + "MenuItem").ToList();
        Assert.Equal(["{Binding RueckgaengigCommand}", "{Binding WiederholenCommand}"],
            eintraege.Select(e => (string?)e.Attribute("Command")));
        Assert.All(eintraege, e => Assert.NotNull(e.Element(wpf + "MenuItem.Icon")));

        var tasten = doc.Descendants(wpf + "KeyBinding")
            .Select(k => ((string?)k.Attribute("Key"), (string?)k.Attribute("Modifiers"), (string?)k.Attribute("Command"))).ToList();
        Assert.Contains(("Z", "Control", "{Binding RueckgaengigTasteCommand}"), tasten);
        Assert.Contains(("Y", "Control", "{Binding WiederholenTasteCommand}"), tasten);
        Assert.Contains(("Z", "Control+Shift", "{Binding WiederholenTasteCommand}"), tasten);
    }

    [Fact]
    public void Tastenkuerzel_und_Handbuch_nennen_Rueckgaengig()
    {
        var gruppe = TastenkuerzelWindow.BauGruppen().Single(g => g.Titel == "Haltungen und Schächte");
        Assert.Equal(["Strg+Z", "Strg+Y", "Strg+Umschalt+Z"], gruppe.Kuerzel.Select(k => k.Taste));
        Assert.Contains("Strg+Z", HandbuchInhalt.Finde("Haltungen").Text);
        Assert.Contains("Strg+Z", HandbuchInhalt.Finde("Schaechte").Text);
    }

    private sealed class SchaltbarerGuard : IShellOperationGuard
    {
        private bool _laeuft;

        public bool Laeuft
        {
            get => _laeuft;
            set
            {
                _laeuft = value;
                OperationAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool CanSaveProjectFromShell => !_laeuft;
        public string ProjectSaveBlockedMessage => "Vorgang läuft";
        public bool AllowsInternalProjectSave => false;
        public bool CanLeaveShellContext => !_laeuft;
        public string LeaveBlockedMessage => "Vorgang läuft";
        public event EventHandler? OperationAvailabilityChanged;
    }
}
