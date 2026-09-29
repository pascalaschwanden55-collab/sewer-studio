using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 5): Eine Objektakte, die ueber die Projektpruefung
/// geoeffnet wird (<see cref="ProjektPruefpunktNavigation.Oeffne"/>), muss denselben
/// Datenaenderungsverlauf tragen wie die Haltungs-/Schachtseite (Aufgabe 16, Rückgängig/Wiederholen)
/// - sonst waeren dort gemachte Aenderungen nicht rueckgaengig machbar. <c>Oeffne</c> zeigt dafuer
/// ein modales Fenster und laesst sich deshalb nicht direkt end-to-end testen; die beiden Tests
/// unten pruefen stattdessen (a) die Verdrahtung im Quelltext und (b) die reale Konsequenz, indem
/// exakt derselbe <c>ObjektaktenDialog.Fabrik(...)</c>-Aufruf mit dem echten Shell-Verlauf
/// nachgebaut wird.
/// </summary>
public sealed class ProjektPruefpunktNavigationVerlaufTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly ServiceProvider _sp;

    public ProjektPruefpunktNavigationVerlaufTests()
    {
        _sp = new ServiceProvider(new AppSettings { EnableRestorePoints = false }, new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"), _loggerFactory);
    }

    public void Dispose() => _loggerFactory.Dispose();

    [Fact]
    public void Oeffne_uebergibt_den_shell_verlauf_an_die_objektakten_fabrik()
    {
        var quelle = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Services", "ProjektPruefpunktNavigation.cs"));
        var start = quelle.IndexOf("ObjektaktenDialog.Fabrik(punkt.Objektart", StringComparison.Ordinal);
        Assert.True(start >= 0, "Der Fabrik-Aufruf in Oeffne wurde nicht gefunden.");
        var ende = quelle.IndexOf(");", start, StringComparison.Ordinal);
        Assert.True(ende > start, "Das Ende des Fabrik-Aufrufs wurde nicht gefunden.");
        var aufruf = quelle[start..ende];

        Assert.Contains("sp.DatenaenderungsVerlauf", aufruf);
    }

    /// <summary>
    /// Baut exakt den Aufruf nach, den <see cref="ProjektPruefpunktNavigation.Oeffne"/> jetzt macht
    /// (<c>ObjektaktenDialog.Fabrik(..., sp.DatenaenderungsVerlauf)</c>), und zeigt: eine darueber
    /// vorgenommene Feldaenderung landet wirklich im Verlauf und ist rueckgaengig machbar - der
    /// bisherige Zustand (kein Verlauf uebergeben) haette hier <c>KannRueckgaengig</c> = false
    /// stehen lassen.
    /// </summary>
    [Fact]
    public void Ueber_die_fabrik_mit_dem_shell_verlauf_geoeffnete_objektakte_ist_rueckgaengig_faehig()
    {
        var projekt = new Project();
        var h = new HaltungRecord();
        projekt.Data.Add(h);
        _sp.DatenaenderungsVerlauf.Binde(projekt);

        var fabrik = ObjektaktenDialog.Fabrik("haltung", () => projekt, _sp.Settings,
            () => true, () => { }, () => { }, _sp.ObjektaktenPakete, _sp.Dialogs,
            _sp.ObjektaktenListenErgaenzungen, _sp.GeoShop, _sp.GeoShopSicherung, _sp.DatenaenderungsVerlauf);

        var vm = fabrik(h.Id);
        Assert.NotNull(vm);

        Assert.False(_sp.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));

        var bearbeitung = vm!.Gruppen.SelectMany(g => g.Felder).First().Bearbeitung;
        var gruppenFeld = FieldCatalog.Objektfelder.Feld("haltung.pipegroup");
        var beton = bearbeitung.ErlaubteEintraege(bearbeitung.Wurzel, gruppenFeld).Single(e => e.Label == "Beton");
        bearbeitung.Schreibe(bearbeitung.Wurzel, gruppenFeld, "", beton.Label, beton);

        Assert.True(_sp.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));

        var ergebnis = _sp.DatenaenderungsVerlauf.Rueckgaengig(DatenaenderungsBereich.Haltungen);
        Assert.True(ergebnis.Angewendet);
    }
}
