using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Plan WG03/WG04 (Prüfung 28.09.2026): Ohne Startzeile im Änderungslog geht nichts ans WebGIS, ein
/// Logausfall stoppt den Lauf vor dem nächsten Schreiben, und ein Projektwechsel ebenso.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    /// <summary>Log im Speicher; ab einer bestimmten Zeile (1-basiert) schlägt es fehl.</summary>
    private sealed class SpeicherLog : IWebGisLaufLog
    {
        public int FehlerAbZeile { get; init; } = int.MaxValue;
        public List<string> Zeilen { get; } = new();
        public string LaufId => "T1";

        public void Schreibe(string zeilen)
        {
            if (Zeilen.Count + 1 >= FehlerAbZeile) throw new WebGisLogException("Datei gesperrt");
            Zeilen.Add(zeilen);
        }

        public bool VersucheSchreibe(string zeilen)
        {
            try { Schreibe(zeilen); return true; }
            catch (WebGisLogException) { return false; }
        }
    }

    private static WebGisExportPosition HaltungMitAenderung(string name, string gid) => new()
    {
        Objektart = WebGisObjektart.Haltung, Bezeichnung = name, GlobalId = gid, RecordId = Guid.NewGuid(),
        GelesenerStand = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.HaltungZustandRef] = "102" },
        Aenderungen = { new WebGisFeldAenderung { RefId = WebGisFeldkarte.HaltungZustandRef, Feld = "Zustand", Alt = "102", Neu = "104" } },
    };

    private static (WebGisExportPlan Plan, FakeClient Client) ZweiHaltungen()
    {
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(HaltungMitAenderung("H1", "G1"));
        plan.Positionen.Add(HaltungMitAenderung("H2", "G2"));
        var client = new FakeClient { Lese = (_, name) => HaltungMitZustand("102", name == "H1" ? "G1" : "G2", name) };
        return (plan, client);
    }

    [Fact]
    public async Task Ohne_startzeile_im_log_geht_nichts_ans_webgis()
    {
        var (plan, client) = ZweiHaltungen();
        var log = new SpeicherLog { FehlerAbZeile = 1 };

        var ausgang = await WebGisSendenAblauf.FuehreAusAsync(
            new WebGisExportUseCase(client), plan, log, "tester", () => true);

        Assert.Equal(WebGisSendenAblauf.Ausgang.KeinStartbeleg, ausgang);
        Assert.Empty(client.Geschrieben);
    }

    [Fact]
    public async Task Logausfall_nach_dem_ersten_objekt_stoppt_vor_dem_zweiten()
    {
        var (plan, client) = ZweiHaltungen();
        // Zeile 1 Start, 2 «geplant H1», 3 «OK H1» scheitert — H1 ist dann schon bestätigt geschrieben.
        var log = new SpeicherLog { FehlerAbZeile = 3 };

        var ausgang = await WebGisSendenAblauf.FuehreAusAsync(
            new WebGisExportUseCase(client), plan, log, "tester", () => true);

        Assert.Equal(WebGisSendenAblauf.Ausgang.LogAusgefallen, ausgang);
        Assert.Single(client.Geschrieben);
        Assert.True(plan.Positionen[0].Geschrieben);   // bestätigt bleibt bestätigt
        Assert.False(plan.Positionen[1].Geschrieben);  // nie gesendet
    }

    [Fact]
    public async Task Vor_jedem_schreiben_steht_der_geplante_schritt_im_log()
    {
        var (plan, client) = ZweiHaltungen();
        var log = new SpeicherLog();

        var ausgang = await WebGisSendenAblauf.FuehreAusAsync(
            new WebGisExportUseCase(client), plan, log, "tester", () => true);

        Assert.Equal(WebGisSendenAblauf.Ausgang.Abgeschlossen, ausgang);
        Assert.Equal(2, client.Geschrieben.Count);
        var geplantH1 = log.Zeilen.FindIndex(z => z.Contains("H1") && z.Contains("GEPLANT"));
        var okH1 = log.Zeilen.FindIndex(z => z.Contains("H1") && z.Contains("| OK"));
        Assert.True(geplantH1 >= 0 && okH1 > geplantH1, string.Join(Environment.NewLine, log.Zeilen));
        Assert.Contains("gestartet", log.Zeilen[0]);
        Assert.Contains("abgeschlossen", log.Zeilen[^1]);
    }

    [Fact]
    public async Task Projektwechsel_waehrend_des_laufs_stoppt_vor_dem_naechsten_schreiben()
    {
        var (plan, client) = ZweiHaltungen();
        var log = new SpeicherLog();
        var abfragen = 0;

        // 1. Abfrage vor dem Start, 2. vor H1 — ab der 3. (vor H2) ist das Projekt gewechselt.
        var ausgang = await WebGisSendenAblauf.FuehreAusAsync(
            new WebGisExportUseCase(client), plan, log, "tester", () => ++abfragen <= 2);

        Assert.Equal(WebGisSendenAblauf.Ausgang.ProjektGewechselt, ausgang);
        Assert.Single(client.Geschrieben);
        Assert.Contains(log.Zeilen, z => z.Contains("ABGEBROCHEN"));
    }

    [Fact]
    public async Task Gewechseltes_projekt_vor_dem_start_sendet_nichts()
    {
        var (plan, client) = ZweiHaltungen();
        var log = new SpeicherLog();

        var ausgang = await WebGisSendenAblauf.FuehreAusAsync(
            new WebGisExportUseCase(client), plan, log, "tester", () => false);

        Assert.Equal(WebGisSendenAblauf.Ausgang.ProjektGewechselt, ausgang);
        Assert.Empty(client.Geschrieben);
        Assert.Empty(log.Zeilen);
    }

    [Fact]
    public void Projektbindung_gilt_nur_fuer_dieselbe_instanz_und_denselben_pfad()
    {
        var a = new Project();
        var b = new Project();
        var bindung = new WebGisProjektBindung(a, @"C:\Projekte\A\projekt.json");

        Assert.True(bindung.Gilt(a, @"C:\Projekte\A\projekt.json"));
        Assert.True(bindung.Gilt(a, @"c:\projekte\a\projekt.json"));
        Assert.False(bindung.Gilt(b, @"C:\Projekte\A\projekt.json"));
        Assert.False(bindung.Gilt(a, @"C:\Projekte\B\projekt.json"));
        Assert.False(bindung.Gilt(null, @"C:\Projekte\A\projekt.json"));
    }
}
