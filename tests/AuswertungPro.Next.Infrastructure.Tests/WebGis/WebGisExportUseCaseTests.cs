using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Der Schreibablauf (WebGisExportUseCase) hatte bis 22.09.2026 keinen einzigen Test. Anlass:
/// Lauf Buerglen 21.09. 15:57 — acht Objekte «Feld Material wurde im WebGIS seit dem Plan
/// geaendert (jetzt '144')», obwohl sich dort nichts geaendert hatte: Der Plan trug den
/// Klartext als Ausgangswert, der Konfliktschutz verglich ihn mit dem Schluessel.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    [Fact]
    public void Abweichende_gespeicherte_globalid_sperrt_export()
    {
        var eingabe = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145", GespeicherteGlobalId = "andere-id",
        };

        var position = WebGisExportPlanBuilder.Baue(eingabe, SchachtStand());

        Assert.NotEmpty(position.Sperren);
        Assert.False(position.Schreibbar);
    }

    [Fact]
    public void Anderer_webgis_name_sperrt_export()
    {
        var eingabe = new WebGisObjektEingabe { Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145" };
        var stand = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "anderer Name" };

        var position = WebGisExportPlanBuilder.Baue(eingabe, stand);

        Assert.NotEmpty(position.Sperren);
        Assert.False(position.Schreibbar);
    }

    [Fact]
    public async Task Katasterwerte_werden_nicht_ins_webgis_zurueckgeschrieben()
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        haltung.SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Kataster, false);
        haltung.SetFieldValue(FieldKeys.Remarks, "GeoShop-Text", FieldSource.Kataster, false);
        haltung.SetFieldValue("Baujahr", "1963", FieldSource.Kataster, false);
        var projekt = new Project();
        projekt.Data.Add(haltung);
        var client = new FakeClient
        {
            Lese = (_, name) => new WebGisLesestand { GlobalId = "G1", Bezeichnung = name },
        };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(projekt);

        Assert.Empty(plan.Positionen.Single().Aenderungen);
    }

    [Fact]
    public async Task Wenn_alle_suchtreffer_fehlen_wird_ein_gemeinsames_suchproblem_gemeldet()
    {
        var projekt = new Project();
        foreach (var name in new[] { "H1", "H2" })
        {
            var haltung = new HaltungRecord();
            haltung.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
            projekt.Data.Add(haltung);
        }

        var plan = await new WebGisExportUseCase(new FakeClient()).BauePlanAsync(projekt);

        Assert.Equal(2, plan.Gesperrte);
        Assert.Contains(plan.Hinweise, hinweis => hinweis.Contains("Kein einziger Name"));
    }
    // ---------------- Gespeicherte GlobalID: direkt lesen, nie ueber den Namen (Pascal 23.09.2026) ----------------
    // Anlass Zone 1.15: Ab 14:32 fand die Namenssuche nichts mehr, alle 182 Objekte waren gesperrt,
    // obwohl ihre GlobalID laengst bekannt war. Mit gespeicherter GlobalID haengt nichts mehr an der Suche.

    private static Project ProjektMitHaltung(string name, string? globalId, string zustand = "4")
    {
        var haltung = new HaltungRecord { WebGisGlobalId = globalId };
        haltung.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        haltung.SetFieldValue(FieldKeys.ConditionClass, zustand, FieldSource.Manual, true);
        var projekt = new Project();
        projekt.Data.Add(haltung);
        return projekt;
    }

    private static WebGisLesestand HaltungStand(string gid, string name)
        => new()
        {
            GlobalId = gid, Bezeichnung = name,
            Felder = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.HaltungZustandRef] = "102" },
        };

    [Fact]
    public async Task Gespeicherte_globalid_liest_und_schreibt_ohne_namenssuche()
    {
        var client = new FakeClient
        {
            Lese = (_, _) => throw new InvalidOperationException("Keine Namenssuche erwartet."),
            LeseUeberId = (_, id) => HaltungStand(id, "80638-80631"),
        };
        var useCase = new WebGisExportUseCase(client);

        var plan = await useCase.BauePlanAsync(ProjektMitHaltung("80638-80631", "31946755-E44D-4714-8DA6-91F56A6AC8FA"));
        var pos = plan.Positionen.Single();
        Assert.True(pos.Schreibbar, string.Join(" | ", pos.Sperren));
        Assert.Equal("31946755-E44D-4714-8DA6-91F56A6AC8FA", pos.GlobalId);

        await useCase.FuehreAusAsync(plan, probelauf: false);

        Assert.True(pos.Geschrieben, pos.SchreibFehler);
        Assert.Equal("31946755-E44D-4714-8DA6-91F56A6AC8FA", Assert.Single(client.Geschrieben).GlobalId);
        Assert.Equal(0, client.NamensSuchen);
        Assert.Equal(3, client.IdLesungen); // Plan, frisch vor dem Schreiben, Nachkontrolle
    }

    [Fact]
    public async Task Ohne_gespeicherte_globalid_bleibt_es_bei_der_namenssuche()
    {
        var client = new FakeClient { Lese = (_, name) => HaltungStand("G1", name) };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(ProjektMitHaltung("H1", globalId: null));

        Assert.True(plan.Positionen.Single().Schreibbar);
        Assert.Equal(1, client.NamensSuchen);
        Assert.Equal(0, client.IdLesungen);
    }

    [Fact]
    public async Task Anderer_name_hinter_der_gespeicherten_globalid_sperrt_und_nennt_beide_namen()
    {
        var client = new FakeClient { LeseUeberId = (_, id) => HaltungStand(id, "80640-80638") };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(ProjektMitHaltung("80638-80631", "G1"));

        var pos = plan.Positionen.Single();
        Assert.False(pos.Schreibbar);
        var sperre = Assert.Single(pos.Sperren);
        Assert.Contains("80640-80638", sperre);
        Assert.Contains("80638-80631", sperre);
        Assert.Equal(0, client.NamensSuchen);
    }

    [Fact]
    public async Task Nicht_lesbare_gespeicherte_globalid_sperrt_ohne_auf_den_namen_auszuweichen()
    {
        var client = new FakeClient
        {
            Lese = (_, name) => HaltungStand("ANDERE", name), // die Suche faende ein anderes Objekt
            LeseUeberId = (_, _) => null,
        };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(ProjektMitHaltung("80638-80631", "G1"));

        var pos = plan.Positionen.Single();
        Assert.False(pos.Schreibbar);
        Assert.Contains("gespeicherten GlobalID", Assert.Single(pos.Sperren));
        Assert.Equal(0, client.NamensSuchen);
    }

    [Fact]
    public async Task Umbenennung_im_webgis_zwischen_plan_und_schreiben_sperrt_das_schreiben()
    {
        var name = "80638-80631";
        var client = new FakeClient { LeseUeberId = (_, id) => HaltungStand(id, name) };
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(ProjektMitHaltung("80638-80631", "G1"));

        name = "80638-99999"; // im WebGIS umbenannt, bevor geschrieben wird
        await useCase.FuehreAusAsync(plan, probelauf: false);

        var pos = plan.Positionen.Single();
        Assert.False(pos.Geschrieben);
        Assert.Contains("80638-99999", pos.SchreibFehler);
        Assert.Empty(client.Geschrieben);
    }

    [Fact]
    public async Task Massnahme_liest_ihr_elternobjekt_ueber_die_gespeicherte_globalid()
    {
        var client = new FakeClient
        {
            Lese = (_, _) => throw new InvalidOperationException("Keine Namenssuche erwartet."),
            LeseUeberId = (_, id) => HaltungStand(id, "80638-80631"),
        };
        var plan = new WebGisExportPlan();
        var elternId = Guid.NewGuid();
        plan.Positionen.Add(new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "80638-80631", GlobalId = "G1",
            GespeicherteGlobalId = "G1", RecordId = elternId,
        });
        var san = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "80638-80631", ElternGlobalId = "G1", ElternRecordId = elternId,
        };
        san.Felder["x"] = "1";
        plan.Sanierungen.Add(san);

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(san.Geschrieben, san.SchreibFehler);
        Assert.Equal(0, client.NamensSuchen);
    }

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

        /// <summary>
        /// Wie ein echter Server: Geschriebene Felder stehen beim naechsten Lesen drin.
        /// Auf false gesetzt bildet der Fake einen Server nach, der ein Feld annimmt und
        /// still verwirft (Buerglen: Tiefe ohne Koten).
        /// </summary>
        public bool SchreibenWirkt { get; set; } = true;

        private readonly Dictionary<string, Dictionary<string, string>> _gespeichert = new(StringComparer.Ordinal);

        public Func<WebGisObjektart, string, WebGisLesestand?> LeseUeberId { get; set; } = (_, _) => null;
        public int NamensSuchen { get; private set; }
        public int IdLesungen { get; private set; }

        public Task<WebGisLesestand?> LeseAsync(WebGisObjektart art, string bezeichnung, CancellationToken ct = default)
        {
            NamensSuchen++;
            return Task.FromResult(MitGespeichertem(Lese(art, bezeichnung)));
        }

        public Task<WebGisLesestand?> LeseUeberGlobalIdAsync(WebGisObjektart art, string globalId, CancellationToken ct = default)
        {
            IdLesungen++;
            return Task.FromResult(MitGespeichertem(LeseUeberId(art, globalId)));
        }

        private WebGisLesestand? MitGespeichertem(WebGisLesestand? stand)
        {
            if (stand is not null && _gespeichert.TryGetValue(stand.GlobalId, out var felder))
                foreach (var (refId, wert) in felder) stand.Felder[refId] = wert;
            return stand;
        }

        public Task<WebGisSchreibErgebnis> SchreibeAsync(WebGisObjektart art, string globalId, IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
        {
            var res = Schreibe(globalId, felder);
            if (res.Erfolg)
            {
                Geschrieben.Add((globalId, felder));
                if (SchreibenWirkt)
                {
                    if (!_gespeichert.TryGetValue(globalId, out var gespeichert))
                        _gespeichert[globalId] = gespeichert = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (refId, wert) in felder) gespeichert[refId] = wert;
                }
            }
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

    // ---------------- Nachkontrolle: kam der Wert wirklich an? ----------------
    // Buerglen 21./22.09.: Schacht 60284, 60105, 60106 — «Tiefe [m] – → 3.22 | OK», dreimal
    // hintereinander, und das Feld blieb jedes Mal leer. Die Tiefe wird im WebGIS aus Sohlen-
    // und Deckelkote gerechnet; fehlen sie, nimmt der Server den Wert an, speichert ihn aber
    // nicht. Ein «OK» im Log fuer etwas, das nicht passiert ist, darf es nicht geben.

    /// <summary>Stand mit frei setzbaren Textfeldern (Tiefe).</summary>
    private static WebGisLesestand SchachtStandMitTiefe(string? tiefe, string gid = "G1")
    {
        var stand = SchachtStand(gid: gid);
        stand.Felder["db8b7f6e-234c-536f-7fbd-3b6d841d8ccd"] = tiefe;
        return stand;
    }

    [Fact]
    public async Task Ein_feld_das_der_server_still_verwirft_wird_gemeldet_statt_als_ok()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "3.22")), SchachtStandMitTiefe(null));
        Assert.Single(pos.Aenderungen);
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        // Der Server nimmt an und meldet Erfolg, das Feld bleibt aber leer.
        var client = new FakeClient { Lese = (_, _) => SchachtStandMitTiefe(null), SchreibenWirkt = false };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.False(pos.Geschrieben);
        Assert.Contains("Tiefe [m]", pos.SchreibFehler);
        Assert.Contains("nicht übernommen", pos.SchreibFehler);
    }

    [Fact]
    public async Task Angekommener_wert_bleibt_ein_erfolg()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "3.22")), SchachtStandMitTiefe(null));
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var client = new FakeClient { Lese = (_, _) => SchachtStandMitTiefe(null) };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(pos.Geschrieben);
        Assert.Null(pos.SchreibFehler);
    }

    [Fact]
    public async Task Nachkontrolle_akzeptiert_dieselbe_zahl_in_anderer_schreibweise()
    {
        // Geschrieben 1.80, der Server speichert 1.8 — das ist angekommen.
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "1.80")), SchachtStandMitTiefe(null));
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var gelesen = 0;
        var client = new FakeClient
        {
            Lese = (_, _) => SchachtStandMitTiefe(gelesen++ == 0 ? null : "1.8"), SchreibenWirkt = false,
        };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(pos.Geschrieben);
    }

    [Fact]
    public async Task Nachkontrolle_die_selbst_scheitert_macht_aus_einem_erfolg_keinen_fehler()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "3.22")), SchachtStandMitTiefe(null));
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var gelesen = 0;
        var client = new FakeClient
        {
            Lese = (_, _) => gelesen++ == 0 ? SchachtStandMitTiefe(null) : null, SchreibenWirkt = false,
        };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(pos.Geschrieben);
        Assert.Contains(pos.Hinweise, h => h.Contains("nicht nachgeprüft"));
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
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2", b), // die Suche liefert den gesuchten Namen
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
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2", b), // die Suche liefert den gesuchten Namen
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
            Lese = (_, b) => HaltungStand(b == "H1" ? "G1" : "G2", b), // die Suche liefert den gesuchten Namen
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

    // ---------------- Material ohne Gruppe im Text: Gruppe aus den Listen des WebGIS ableiten ----------------
    // Anlass 23.09.2026 (Haltung 386227-80538): SewerStudio fuehrt nur «Polypropylen», das WebGIS steht auf
    // Beton. Im Browser muss man zuerst die Gruppe wechseln; SewerStudio tat gar nichts (nur Hinweis).

    private static FakeClient ClientMitGruppenlisten() => new()
    {
        KatalogListe = (_, refId, filter) => refId != MaterialDetailRef ? null : filter switch
        {
            "1" => new List<(string, string)> { ("101", "Beton, unbekannt"), ("104", "Beton, Fertigteil") },
            "3" => new List<(string, string)> { ("301", "Kunststoff, PVC"), ("305", "Polypropylen") },
            _ => new List<(string, string)>(),
        },
    };

    [Fact]
    public async Task Detail_ohne_gruppe_im_text_setzt_die_einzige_passende_gruppe_mit()
    {
        var e = Schacht(("Material", "Polypropylen"));
        var stand = SchachtStandMitMaterial(("101", "Beton, unbekannt"), ("104", "Beton, Fertigteil"));
        stand.Felder[MaterialHauptRef] = "1"; // im WebGIS steht Beton
        stand.Felder[MaterialDetailRef] = "104";

        await new WebGisExportUseCase(ClientMitGruppenlisten()).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.Contains(pos.Aenderungen, a => a.RefId == MaterialDetailRef && a.Neu == "305");
        Assert.Contains(pos.Aenderungen, a => a.RefId == MaterialHauptRef && a.Neu == "3");
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("nicht vorhanden"));
    }

    [Fact]
    public async Task Detail_in_mehreren_gruppen_wird_nicht_geraten()
    {
        var e = Schacht(("Material", "Polypropylen"));
        var stand = SchachtStandMitMaterial(("101", "Beton, unbekannt"));
        stand.Felder[MaterialHauptRef] = "1";
        var client = new FakeClient
        {
            KatalogListe = (_, refId, filter) => refId == MaterialDetailRef && filter is "0" or "3"
                ? new List<(string, string)> { ("305", "Polypropylen") }
                : new List<(string, string)>(),
        };

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialDetailRef || a.RefId == MaterialHauptRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("Polypropylen") && h.Contains("mehreren"));
    }

    [Fact]
    public async Task Detail_in_keiner_gruppe_bleibt_beim_hinweis()
    {
        var e = Schacht(("Material", "Zement"));
        var stand = SchachtStandMitMaterial(("101", "Beton, unbekannt"));
        stand.Felder[MaterialHauptRef] = "1";

        await new WebGisExportUseCase(ClientMitGruppenlisten()).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialDetailRef || a.RefId == MaterialHauptRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("Zement") && h.Contains("nicht vorhanden"));
    }
}
