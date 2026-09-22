using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Der Schreibablauf (WebGisExportUseCase) hatte bis 22.09.2026 keinen einzigen Test. Anlass:
/// Lauf Buerglen 21.09. 15:57 — acht Objekte «Feld Material wurde im WebGIS seit dem Plan
/// geaendert (jetzt '144')», obwohl sich dort nichts geaendert hatte: Der Plan trug den
/// Klartext als Ausgangswert, der Konfliktschutz verglich ihn mit dem Schluessel.
/// </summary>
public sealed class WebGisExportUseCaseTests
{
    private const string FormRef = "20888829-e165-1c4f-e997-0991e22e9be0";
    private const string MaterialHauptRef = "57efe6f1-4e76-3844-d13b-4c6dc0e1301f";
    private const string MaterialDetailRef = "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728";

    /// <summary>Fake des Katasterzugriffs: liefert vorgegebene Staende, merkt sich jedes Schreiben.</summary>
    private sealed class FakeClient : IGeonisWebGisClient
    {
        public Func<WebGisObjektart, string, WebGisLesestand?> Lese { get; set; } = (_, _) => null;
        public Func<string, IReadOnlyDictionary<string, string>, WebGisSchreibErgebnis> Schreibe { get; set; }
            = (_, _) => WebGisSchreibErgebnis.Ok();
        public Func<WebGisObjektart, string, string, IReadOnlyList<(string Key, string Text)>?> KatalogListe { get; set; }
            = (_, _, _) => null;

        public List<(string GlobalId, IReadOnlyDictionary<string, string> Felder)> Geschrieben { get; } = new();
        public int KatalogListenAufrufe { get; private set; }
        public string? LetzterSubtyp { get; private set; }

        public Task<WebGisLesestand?> LeseAsync(WebGisObjektart art, string bezeichnung, CancellationToken ct = default)
            => Task.FromResult(Lese(art, bezeichnung));

        public Task<WebGisSchreibErgebnis> SchreibeAsync(WebGisObjektart art, string globalId, IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
        {
            var res = Schreibe(globalId, felder);
            if (res.Erfolg) Geschrieben.Add((globalId, felder));
            return Task.FromResult(res);
        }

        public Task<WebGisSanierungKatalog?> LeseSanierungKatalogAsync(WebGisObjektart art, string elternGlobalId, CancellationToken ct = default)
            => Task.FromResult<WebGisSanierungKatalog?>(null);

        public Task<WebGisSchreibErgebnis> ErstelleSanierungAsync(WebGisObjektart art, string elternGlobalId, IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
            => Task.FromResult(WebGisSchreibErgebnis.Ok("1"));

        public Task<IReadOnlyList<(string Key, string Text)>?> LeseKatalogListeAsync(WebGisObjektart art, string refId, string filter, string? subtyp = null, CancellationToken ct = default)
        {
            KatalogListenAufrufe++;
            LetzterSubtyp = subtyp;
            return Task.FromResult(KatalogListe(art, refId, filter));
        }
    }

    private static WebGisLesestand SchachtStand(string form = "0", string gid = "G1")
    {
        var f = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [WebGisFeldkarte.SchachtZustandRef] = "103",
            [WebGisFeldkarte.SchachtSanierungsbedarfRef] = "104",
            [WebGisFeldkarte.SchachtBemerkungRef] = "",
            [FormRef] = form,
        };
        var k = new Dictionary<string, List<(string Key, string Text)>>(StringComparer.Ordinal)
        {
            [FormRef] = new() { ("0", "Unbekannt"), ("1", "Rund"), ("102", "Oval") },
        };
        return new WebGisLesestand { GlobalId = gid, Bezeichnung = "525145", Felder = f, Kataloge = k };
    }

    private static WebGisObjektEingabe Schacht(params (string Feld, string Wert)[] handwerte)
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145", RecordId = Guid.NewGuid(),
            Zustandsklasse = "3", Saniert = false,
        };
        foreach (var (f, w) in handwerte) e.Handwerte[f] = w;
        return e;
    }

    /// <summary>Eine schreibbare Position mit genau einer Zustandsaenderung (102 -> 104).</summary>
    private static WebGisExportPosition ZustandsPosition(string bezeichnung, string gid)
    {
        var p = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = bezeichnung, GlobalId = gid };
        p.Aenderungen.Add(new WebGisFeldAenderung { RefId = WebGisFeldkarte.HaltungZustandRef, Feld = "Zustand", Alt = "102", Neu = "104" });
        return p;
    }

    private static WebGisLesestand HaltungStand(string gid)
        => new()
        {
            GlobalId = gid, Bezeichnung = "H",
            Felder = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.HaltungZustandRef] = "102" },
        };

    // ---------------- A1: Konfliktschutz vergleicht Schluessel mit Schluessel ----------------

    [Fact]
    public async Task Unveraenderter_stand_eines_auswahlfelds_ist_kein_konflikt()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Schachtform", "Rund")), SchachtStand(form: "0"));
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Form" && a.Neu == "1");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "0") }; // im WebGIS nichts passiert

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Null(pos.SchreibFehler);
        Assert.True(pos.Geschrieben);
        var geschrieben = Assert.Single(client.Geschrieben);
        Assert.Equal("1", geschrieben.Felder[FormRef]);
    }

    [Fact]
    public async Task Inzwischen_geaenderter_stand_eines_auswahlfelds_sperrt_das_objekt()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Schachtform", "Rund")), SchachtStand(form: "0"));
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "102") }; // jemand hat Oval gesetzt

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.False(pos.Geschrieben);
        Assert.Contains("Form", pos.SchreibFehler);
        Assert.Empty(client.Geschrieben);
    }

    [Fact]
    public void Bericht_und_uebersicht_zeigen_den_alten_wert_als_klartext()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Schachtform", "Rund")), SchachtStand(form: "0"));
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);

        Assert.Contains("Form: Unbekannt → Rund", WebGisExportBericht.Details(plan, mitErgebnis: false));
        var objekt = Assert.Single(WebGisUebersicht.Aus(plan).Objekte);
        Assert.Equal("Unbekannt", objekt.Zeilen[0].Alt);
    }

    // ---------------- C1: ein Fehler stoppt die uebrigen nicht, jeder Schritt wird gemeldet ----------------

    [Fact]
    public async Task Ein_schreibfehler_stoppt_die_uebrigen_objekte_nicht()
    {
        var p1 = ZustandsPosition("H1", "G1");
        var p2 = ZustandsPosition("H2", "G2");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(p1); plan.Positionen.Add(p2);
        var client = new FakeClient
        {
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2"),
            Schreibe = (gid, _) => gid == "G1" ? throw new HttpRequestException("Verbindung unterbrochen") : WebGisSchreibErgebnis.Ok(),
        };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.False(p1.Geschrieben);
        Assert.Contains("Verbindung unterbrochen", p1.SchreibFehler);
        Assert.True(p2.Geschrieben);
    }

    [Fact]
    public async Task Nach_jedem_objekt_wird_der_beobachter_mit_dem_ergebnis_gerufen()
    {
        var p1 = ZustandsPosition("H1", "G1");
        var p2 = ZustandsPosition("H2", "G2");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(p1); plan.Positionen.Add(p2);
        var client = new FakeClient
        {
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2"),
            Schreibe = (gid, _) => gid == "G1" ? WebGisSchreibErgebnis.Fehlgeschlagen("Regelverletzung") : WebGisSchreibErgebnis.Ok(),
        };
        var gesehen = new List<(string Objekt, bool Ok, string? Fehler)>();

        await new WebGisExportUseCase(client).FuehreAusAsync(
            plan, probelauf: false, nachObjekt: p => gesehen.Add((p.Bezeichnung, p.Geschrieben, p.SchreibFehler)));

        Assert.Equal(2, gesehen.Count);
        Assert.Equal(("H1", false, "Regelverletzung"), gesehen[0]);
        Assert.Equal(("H2", true, null), gesehen[1]);
    }

    [Fact]
    public async Task Abgelaufene_sitzung_bricht_ab_meldet_aber_die_betroffene_position_vorher()
    {
        var p1 = ZustandsPosition("H1", "G1");
        var p2 = ZustandsPosition("H2", "G2");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(p1); plan.Positionen.Add(p2);
        var client = new FakeClient
        {
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2"),
            Schreibe = (gid, _) => gid == "G2" ? throw new WebGisSitzungException("Token abgelaufen") : WebGisSchreibErgebnis.Ok(),
        };
        var gesehen = new List<string>();

        await Assert.ThrowsAsync<WebGisSitzungException>(() => new WebGisExportUseCase(client).FuehreAusAsync(
            plan, probelauf: false, nachObjekt: p => gesehen.Add(p.Bezeichnung)));

        Assert.True(p1.Geschrieben);
        Assert.Contains("Sitzung", p2.SchreibFehler);
        Assert.Equal(new[] { "H1", "H2" }, gesehen); // auch das abgebrochene Objekt wird gemeldet, damit es im Log steht
    }

    // ---------------- A2: gruppenabhaengige Detail-Liste (Material) ueber die Gruppe nachladen ----------------

    private static WebGisLesestand SchachtStandMitMaterial(params (string Key, string Text)[] detailListe)
    {
        var stand = SchachtStand();
        stand.Felder[MaterialHauptRef] = "3";     // im WebGIS steht Kunststoff
        stand.Felder[MaterialDetailRef] = "301";
        stand.Kataloge[MaterialHauptRef] = new() { ("0", "Unbekannt"), ("1", "Beton"), ("3", "Kunststoff") };
        stand.Kataloge[MaterialDetailRef] = new(detailListe);
        return stand;
    }

    [Fact]
    public async Task Detail_einer_anderen_gruppe_wird_ueber_die_gruppe_nachgeladen()
    {
        var e = Schacht(("Material", "Beton, Fertigteil"));
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC")); // die Maske zeigt nur die Kunststoff-Details
        var client = new FakeClient
        {
            KatalogListe = (art, refId, filter) => refId == MaterialDetailRef && filter == "1"
                ? new List<(string, string)> { ("101", "Beton, unbekannt"), ("104", "Beton, Fertigteil") }
                : null,
        };

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.Contains(pos.Aenderungen, a => a.RefId == MaterialDetailRef && a.Neu == "104");
        Assert.Contains(pos.Aenderungen, a => a.RefId == MaterialHauptRef && a.Neu == "1");
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("nicht vorhanden"));
    }

    [Fact]
    public async Task Detail_der_aktuellen_gruppe_braucht_kein_nachladen()
    {
        var e = Schacht(("Material", "Kunststoff, PVC"));
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC"), ("302", "Kunststoff, PE"));
        stand.Felder[MaterialDetailRef] = "302";
        var client = new FakeClient();

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.Equal(0, client.KatalogListenAufrufe);
        Assert.Contains(pos.Aenderungen, a => a.RefId == MaterialDetailRef && a.Neu == "301");
    }

    [Fact]
    public async Task Serverfehler_beim_nachladen_sperrt_nicht_sondern_meldet()
    {
        // 22.09.2026 im Programm: «Could not load form's default form.» beim Nachladen — vorher
        // brach damit die GANZE Pruefung ab, ohne Bericht.
        var e = Schacht(("Material", "Beton, Fertigteil"));
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC"));
        var client = new FakeClient
        {
            KatalogListe = (_, _, _) => throw new WebGisAntwortException("Could not load form's default form."),
        };

        var hinweise = await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.Contains(hinweise, h => h.Contains("Could not load form's default form."));
        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialDetailRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("nicht vorhanden"));
    }

    [Fact]
    public async Task Nachladen_gibt_den_subtyp_der_maske_weiter()
    {
        var e = Schacht(("Material", "Beton, Fertigteil"));
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC"));
        stand.Subtyp = "typ:1";
        var client = new FakeClient();

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);

        Assert.Equal(1, client.KatalogListenAufrufe);
        Assert.Equal("typ:1", client.LetzterSubtyp);
    }

    [Fact]
    public async Task Ohne_nachladbare_gruppenliste_bleibt_das_feld_stehen_und_wird_gemeldet()
    {
        var e = Schacht(("Material", "Beton, Fertigteil"));
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC"));
        var client = new FakeClient(); // KatalogListe liefert null

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialDetailRef);
        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialHauptRef); // kein halbes Paar
        Assert.Contains(pos.Hinweise, h => h.Contains("Beton, Fertigteil") && h.Contains("nicht vorhanden"));
    }
}
