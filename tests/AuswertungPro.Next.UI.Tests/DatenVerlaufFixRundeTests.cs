using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optik Aufgabe 16, Fix-Runde 1: Zellen-Erfassung, Sperren der Uebernahmen, Hinweisarten, offene
/// Eingabe, Abmelden beim Schliessen und abgedocktes Fenster — echte Shell und echte Seiten.
/// </summary>
public sealed class DatenVerlaufFixRundeTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;
    private readonly List<(string Text, ToastSeverity Art)> _toasts = [];

    public DatenVerlaufFixRundeTests()
    {
        _services = new ServiceProvider(new AppSettings { EnableRestorePoints = false }, new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"), _loggerFactory);
        _services.Toasts.AttachSink((text, art, _, _) => _toasts.Add((text, art)));
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
        return h;
    }

    private void Eingabe(HaltungRecord h, string wert)
    {
        using var _ = _services.DatenaenderungsVerlauf.Erfasse(h, FieldKeys.PipeMaterial);
        h.SetFieldValue(FieldKeys.PipeMaterial, wert, FieldSource.Manual, userEdited: true);
    }

    private sealed class Zaehler : IDisposable
    {
        public int Anzahl { get; private set; }
        public void Dispose() => Anzahl++;
    }

    private static void AufEigenemFaden(Action<Dispatcher> aktion)
    {
        Exception? fehler = null;
        var faden = new Thread(() =>
        {
            try { aktion(Dispatcher.CurrentDispatcher); }
            catch (Exception ex) { fehler = ex; }
        });
        faden.Start();
        faden.Join();
        if (fehler is not null)
            throw new Xunit.Sdk.XunitException(fehler.ToString());
    }

    [Fact]
    public void Zellenerfassung_schliesst_einen_vorgemerkten_Bereich_beim_naechsten_Beginn()
        => AufEigenemFaden(dispatcher =>
        {
            var zelle = new DatenVerlaufZellErfassung();
            var erste = new Zaehler();
            var zweite = new Zaehler();

            zelle.Beginne(erste);
            zelle.BeendeNachCommit(dispatcher); // nur vorgemerkt: der Dispatcher laeuft hier nicht
            Assert.Equal(0, erste.Anzahl);

            zelle.Beginne(zweite); // die naechste Zelle darf nicht Teil des alten Schritts werden
            Assert.Equal(1, erste.Anzahl);
            Assert.Equal(0, zweite.Anzahl);

            zelle.Beende(); // Abbruch/Escape: sofort geschlossen
            Assert.Equal(1, zweite.Anzahl);
            zelle.Beende();
            Assert.Equal(1, zweite.Anzahl);
        });

    [Fact]
    public void Zellenerfassung_mit_dem_echten_Verlauf_ergibt_einen_Schritt()
    {
        var h = OeffneHaltungen();
        AufEigenemFaden(dispatcher =>
        {
            var verlauf = _services.DatenaenderungsVerlauf;
            var zelle = new DatenVerlaufZellErfassung();
            zelle.Beginne(verlauf.Erfasse(h, FieldKeys.PipeMaterial, DatenaenderungsVerlauf.ZellSchrittFelder(FieldKeys.PipeMaterial)));
            h.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Manual, true);
            zelle.BeendeNachCommit(dispatcher);
            Assert.True(verlauf.EingabeOffen);
            Assert.False(_shell.RueckgaengigTasteCommand.CanExecute(null)); // Strg+Z wartet auf den Commit

            zelle.Beende();

            Assert.False(verlauf.EingabeOffen);
            Assert.True(_shell.RueckgaengigTasteCommand.CanExecute(null));
            Assert.Equal("Rohrmaterial 10001-10002", verlauf.RueckgaengigBeschreibung(DatenaenderungsBereich.Haltungen));
        });
    }

    [Fact]
    public void Ein_nicht_mehr_moeglicher_Schritt_meldet_eine_Warnung()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");
        h.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Grundbuch, userEdited: true);

        _shell.RueckgaengigCommand.Execute(null);

        var (text, art) = Assert.Single(_toasts, t => t.Text.Contains("nicht möglich"));
        Assert.Equal(ToastSeverity.Warning, art);
        Assert.Equal("Steinzeug", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Leeren_mit_Eintraegen_meldet_den_Grund()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");

        _shell.ReplaceProject(new Project { Name = "Anderes" });

        var (text, art) = Assert.Single(_toasts);
        Assert.Equal(ToastSeverity.Info, art);
        Assert.Equal($"Rückgängig ist nicht mehr möglich: {DatenaenderungsVerlauf.GrundProjekt}.", text);
    }

    [Fact]
    public void Schliessen_der_Shell_meldet_alles_ab()
    {
        var h = OeffneHaltungen();
        Eingabe(h, "PVC");

        _shell.Dispose();

        var verlauf = _services.DatenaenderungsVerlauf;
        Assert.False(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        Assert.Empty(_toasts); // beim Schliessen kein Hinweis mehr
        Eingabe(h, "Beton"); // nicht mehr an ein Projekt gebunden: nichts wird erfasst
        Assert.False(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
    }

    [Fact]
    public async Task Uebernahmen_in_der_Objektakte_leeren_den_Verlauf_nur_bei_Erfolg()
    {
        var h = OeffneHaltungen();
        var verlauf = _services.DatenaenderungsVerlauf;

        Eingabe(h, "PVC");
        Assert.False(ObjektaktenDialog.MitSperre(() => false, verlauf, DatenaenderungsVerlauf.GrundPaket)());
        Assert.True(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        Assert.True(ObjektaktenDialog.MitSperre(() => true, verlauf, DatenaenderungsVerlauf.GrundPaket)());
        Assert.False(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));

        Eingabe(h, "Beton");
        Assert.False(await ObjektaktenDialog.MitSperre(() => Task.FromResult(false), verlauf)());
        Assert.True(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        Assert.True(await ObjektaktenDialog.MitSperre(() => Task.FromResult(true), verlauf)());
        Assert.False(verlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
    }

    [Fact]
    public void Abgedocktes_Fenster_hat_dieselben_Tasten()
    {
        OeffneHaltungen();
        var seite = Assert.IsType<DataPageViewModel>(_shell.CurrentPage);
        Assert.Same(_shell.RueckgaengigTasteCommand, seite.RueckgaengigTasteCommand);
        Assert.Same(_shell.WiederholenTasteCommand, seite.WiederholenTasteCommand);

        var pfad = Path.Combine(TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI", "Views", "Windows", "FloatingGridWindow.xaml");
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var tasten = XDocument.Load(pfad).Descendants(wpf + "KeyBinding")
            .Select(k => ((string?)k.Attribute("Key"), (string?)k.Attribute("Modifiers"), (string?)k.Attribute("Command"))).ToList();
        Assert.Contains(("Z", "Control", "{Binding RueckgaengigTasteCommand}"), tasten);
        Assert.Contains(("Y", "Control", "{Binding WiederholenTasteCommand}"), tasten);
        Assert.Contains(("Z", "Control+Shift", "{Binding WiederholenTasteCommand}"), tasten);
    }

    /// <summary>Der Einstieg der Export-Seite hat keinen eigenen Rueckruf (geschuetzte Datei): Die Sperre
    /// sitzt deshalb im gemeinsamen Ablauf, direkt bei der erfolgreichen Uebernahme.</summary>
    [Fact]
    public void WebGis_Holen_leert_den_Verlauf_in_jedem_Einstieg()
    {
        var code = File.ReadAllText(Path.Combine(TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI", "Services", "WebGisHolenAblauf.cs"));
        var block = code[code.IndexOf("if (uebernommen > 0)", StringComparison.Ordinal)..];
        block = block[..block.IndexOf("_geaendert?.Invoke();", StringComparison.Ordinal)];
        Assert.Contains("shell.DatenVerlauf.Leere(", block);
    }
}
