using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Robustheit des Schreibwegs gegen Antworten, die kein Erfolg sind. Anlass: Pruefung
/// 22.09.2026 — eine Antwort ohne Erfolgsnachweis galt als geschrieben, eine abgelaufene
/// Sitzung bei der Suche als «nicht gefunden», ein geplantes Feld ohne Komponente fiel still
/// weg. Alle drei Muster hatten am 21.09. schon einmal zu falschen Log-Eintraegen gefuehrt.
/// </summary>
public sealed class GeonisWebGisClientRobustheitTests
{
    private static WebGisZugang Zugang() => new()
    {
        BasisUrl = "https://example.test",
        Projekt = "awu_abw_edit",
        Datenquelle = "awu_abw",
        JSessionId = "SESS",
        SynSessionId = "SYN",
        SynLogin = "pascal.aschwanden",
        SynGroups = "G_awu_rw",
    };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, string> _antwort;
        public string? LetzterSaveBody { get; private set; }
        public int SaveAufrufe { get; private set; }
        public FakeHandler(Func<HttpRequestMessage, string, string> antwort) => _antwort = antwort;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var url = request.RequestUri!.ToString();
            if (url.Contains("saveData")) { LetzterSaveBody = body; SaveAufrufe++; }
            var text = _antwort(request, body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(text, Encoding.UTF8, "application/json")
            };
        }
    }

    private const string ZustandRef = WebGisFeldkarte.HaltungZustandRef;
    private const string BemRef = WebGisFeldkarte.HaltungBemerkungRef;
    private const string Gid = "81D76B9E-2E83-40B2-B1F7-608BABE4E89A";
    private const string Faultstring = "{\"faultstring\":\"Das empfangene Authentifizierungs-Token ist abgelaufen\"}";
    private const string Html = "<html><body>Anmeldung erforderlich</body></html>";
    private const string Gespeichert = "{\"newId\":null,\"file\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";

    private static string LayoutJson() =>
        "[{\"components\":[]}," +
        "{\"objectKeyValue\":\"g1\",\"components\":[" +
        "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102,\"keys\":[102,104],\"values\":[\"Z2\",\"Z4\"]}," +
        "{\"refId\":\"" + BemRef + "\",\"value\":\"\"}" +
        "]}]";

    private static string SuggestionJson(string name) =>
        "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[{\"record\":[{\"record\":[" +
        "{\"jsxtext\":\"" + name + ", Sammelkanal\",\"jsxid\":\"FID1\"}" +
        "]}]}]}}}]}";

    private static string ResultsJson(string gid) =>
        "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[{" +
        "\"extapp0_target\":\"https://x/AttributeEditor/indexWebOffice.aspx?table=awk_haltung&id=%7b" + gid + "%7d\"" +
        "}]}}}]}";

    private static string Suche(HttpRequestMessage req, string body)
        => body.Contains("GET_RESULTS") ? ResultsJson(Gid) : SuggestionJson("525145-505377");

    private static GeonisWebGisClient Client(FakeHandler h)
        => new(new HttpClient(h), () => Zugang());

    // ---------------- B1: Erfolg nur mit Erfolgsnachweis des Servers ----------------

    [Theory]
    [InlineData(Html)]                 // Anmelde-/Proxy-/WAF-Seite
    [InlineData("")]                   // leerer Rumpf
    [InlineData("[]")]                 // gueltiges JSON, aber kein Objekt
    [InlineData("null")]
    [InlineData("{}")]                 // Objekt ohne jeden Erfolgsnachweis
    [InlineData("{\"ok\":true}")]      // erfundene Form, die der Server nie liefert
    public async Task SchreibeAsync_meldet_keinen_erfolg_ohne_erfolgsnachweis_des_servers(string antwort)
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return antwort;
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" });

        Assert.False(res.Erfolg);
        Assert.False(string.IsNullOrWhiteSpace(res.Fehler));
    }

    [Fact]
    public async Task SchreibeAsync_erfolg_nur_mit_bestaetigung_des_servers()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return Gespeichert;
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" });

        Assert.True(res.Erfolg);
    }

    // ---------------- B2: geplantes Feld ohne Komponente -> nichts schreiben ----------------

    [Fact]
    public async Task SchreibeAsync_bricht_ab_wenn_ein_geplantes_feld_keine_komponente_hat()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return Gespeichert;
            return "{}";
        });
        const string fremd = "00000000-0000-0000-0000-0000deadbeef";

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1",
            new Dictionary<string, string> { [ZustandRef] = "104", [fremd] = "1" });

        Assert.False(res.Erfolg);
        Assert.Contains(fremd, res.Fehler);
        Assert.Equal(0, h.SaveAufrufe); // nichts Halbes schreiben
    }

    // ---------------- WG01 (Pruefung 28.09.2026): die vier bestaetigten Randfaelle ----------------

    private const string ElternGid = "67BA283D-24AE-4218-86E9-E82442A91D97";

    /// <summary>Leere Sanierungsmaske OHNE das Sanierungsjahr: Art (Vorgabe 4), Status, Verfahren.</summary>
    private static string LeereMaskeOhneJahr() =>
        "{\"objectKeyValue\":null,\"components\":[" +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.ArtRef + "\",\"keySelected\":4}," +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.StatusRef + "\",\"keySelected\":null}," +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"keySelected\":null}" +
        "]}";

    private static string LeereMaskeVollstaendig() =>
        "{\"objectKeyValue\":null,\"components\":[" +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.ArtRef + "\",\"keySelected\":4}," +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.StatusRef + "\",\"keySelected\":null}," +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"keySelected\":null}," +
        "{\"refId\":\"" + WebGisSanierungFeldkarte.SanierungsjahrRef + "\",\"value\":null}" +
        "]}";

    private static Dictionary<string, string> MassnahmeFelder() => new()
    {
        [WebGisSanierungFeldkarte.StatusRef] = "1",
        [WebGisSanierungFeldkarte.SanierungsjahrRef] = "2026-01-01T00:00:00.000Z",
    };

    [Fact]
    public async Task ErstelleSanierungAsync_sendet_nichts_wenn_ein_geplantes_feld_in_der_maske_fehlt()
    {
        // Heute genuegte EIN gesetztes Feld: Das Jahr fiel still weg, die Massnahme ging halb hinaus.
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeereMaskeOhneJahr();
            if (url.Contains("saveData")) return "{\"newId\":\"66921\",\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });

        var res = await Client(h).ErstelleSanierungAsync(WebGisObjektart.Haltung, ElternGid, MassnahmeFelder());

        Assert.False(res.Erfolg);
        Assert.Contains(WebGisSanierungFeldkarte.SanierungsjahrRef, res.Fehler);
        Assert.Equal(0, h.SaveAufrufe);
    }

    [Theory]
    [InlineData("{\"message\":\"Validation failed\"}")]
    [InlineData("{\"newId\":null}")]
    [InlineData("{\"isFailure\":\"false\"}")]
    public async Task SchreibeAsync_unklare_antwort_ist_kein_erfolg(string antwort)
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return antwort;
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" });

        Assert.False(res.Erfolg);
    }

    [Theory]
    [InlineData("{\"newId\":null}")]
    [InlineData("{\"newId\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}")]
    [InlineData("{\"newId\":\"\",\"isFailure\":false}")]
    [InlineData("{\"message\":\"Validation failed\"}")]
    public async Task ErstelleSanierungAsync_ohne_neue_id_ist_kein_erfolg(string antwort)
    {
        // Ohne neue Kennung ist nicht belegt, dass eine Massnahme entstanden ist.
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeereMaskeVollstaendig();
            if (url.Contains("saveData")) return antwort;
            return "{}";
        });

        var res = await Client(h).ErstelleSanierungAsync(WebGisObjektart.Haltung, ElternGid, MassnahmeFelder());

        Assert.False(res.Erfolg);
    }

    [Fact]
    public async Task ErstelleSanierungAsync_bestaetigte_neue_id_ist_erfolg()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeereMaskeVollstaendig();
            if (url.Contains("saveData")) return "{\"newId\":\"66921\",\"file\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });

        var res = await Client(h).ErstelleSanierungAsync(WebGisObjektart.Haltung, ElternGid, MassnahmeFelder());

        Assert.True(res.Erfolg, res.Fehler);
        Assert.Equal("66921", res.NeueId);
    }

    [Fact]
    public async Task SchreibeAsync_doppelte_komponente_ersetzt_kein_fehlendes_feld()
    {
        // Die Maske liefert den Zustand zweimal, die Bemerkung gar nicht. Der alte Anzahlvergleich
        // (2 Komponenten fuer 2 Felder) liess das als vollstaendig durch.
        var layout =
            "[{\"components\":[]}," +
            "{\"objectKeyValue\":\"g1\",\"components\":[" +
            "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102}," +
            "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102}" +
            "]}]";
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return layout;
            if (url.Contains("saveData")) return Gespeichert;
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1",
            new Dictionary<string, string> { [ZustandRef] = "104", [BemRef] = "Saniert 2026" });

        Assert.False(res.Erfolg);
        Assert.Equal(0, h.SaveAufrufe);
    }

    // ---------------- B3: Sitzung auf JEDEM Aufruf pruefen ----------------

    [Fact]
    public async Task LeseAsync_abgelaufene_sitzung_bei_der_suche_wirft_sitzungsfehler()
    {
        var h = new FakeHandler((req, body) => req.RequestUri!.ToString().Contains("synserver") ? Faultstring : "{}");
        await Assert.ThrowsAsync<WebGisSitzungException>(
            () => Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));
    }

    [Fact]
    public async Task LeseAsync_html_statt_json_bei_der_suche_wirft_sitzungsfehler_statt_jsonfehler()
    {
        var h = new FakeHandler((req, body) => req.RequestUri!.ToString().Contains("synserver") ? Html : "{}");
        await Assert.ThrowsAsync<WebGisSitzungException>(
            () => Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));
    }

    [Fact]
    public async Task LeseAsync_html_beim_layout_wirft_sitzungsfehler()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver")) return Suche(req, body);
            if (url.Contains("getLayoutDataCombined")) return Html;
            return "{}";
        });
        await Assert.ThrowsAsync<WebGisSitzungException>(
            () => Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));
    }

    [Fact]
    public async Task SchreibeAsync_faultstring_beim_speichern_ist_kein_erfolg()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return Faultstring;
            return "{}";
        });
        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" });
        Assert.False(res.Erfolg);
        Assert.Contains("Sitzung", res.Fehler);
    }

    // ---------------- B4: Verfahrensliste je Art — ohne sie kein Katalog ----------------

    private static string LeeresSanierungsobjektJson() =>
        "{\"objectKeyValue\":null,\"objectKeyField\":\"objectid\",\"subtype\":[{\"name\":\"art\",\"value\":\"4\"}]," +
        "\"dataReadonly\":false,\"components\":[" +
        "{\"values\":[\"Reparatur\",\"Renovierung\"],\"keys\":[2,4],\"keySelected\":4,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.ArtRef + "\",\"missingValue\":false}," +
        "{\"values\":[\"Kurzrohrverfahren\",\"Schlauchverfahren\"],\"keys\":[13,27],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"missingValue\":false}" +
        "]}";

    private static string VerfahrenJson(string a, string ka, string b, string kb) =>
        "{\"components\":[{\"values\":[\"" + a + "\",\"" + b + "\"],\"keys\":[" + ka + "," + kb + "],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"missingValue\":false}]}";

    [Fact]
    public async Task LeseSanierungKatalogAsync_liefert_null_wenn_eine_verfahrensliste_nicht_ladbar_ist()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeeresSanierungsobjektJson();
            if (url.Contains("getControlValues"))
                return url.Contains("filter=2") ? "{}" : VerfahrenJson("Kurzrohrverfahren", "13", "Schlauchverfahren", "27");
            return "{}";
        });

        var k = await Client(h).LeseSanierungKatalogAsync(WebGisObjektart.Haltung, Gid);

        Assert.Null(k); // halber Katalog = kein Katalog; jede Massnahme wird gesperrt statt geraten
    }

    [Fact]
    public async Task LeseSanierungKatalogAsync_abgelaufene_sitzung_beim_nachladen_wirft()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeeresSanierungsobjektJson();
            if (url.Contains("getControlValues")) return Faultstring;
            return "{}";
        });
        await Assert.ThrowsAsync<WebGisSitzungException>(
            () => Client(h).LeseSanierungKatalogAsync(WebGisObjektart.Haltung, Gid));
    }

    // ---------------- A2: gruppenabhaengige Auswahlliste der Objektmaske nachladen ----------------

    [Fact]
    public async Task LeseKatalogListeAsync_fragt_getControlValues_mit_tabelle_refid_und_filter()
    {
        const string detailRef = "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728";
        string? gefragteUrl = null;
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getControlValues"))
            {
                gefragteUrl = url;
                return "{\"components\":[{\"values\":[\"Beton, unbekannt\",\"Beton, Fertigteil\"],\"keys\":[101,104],\"keySelected\":null,\"value\":null,\"refId\":\"" + detailRef + "\",\"missingValue\":false}]}";
            }
            return "{}";
        });

        var liste = await Client(h).LeseKatalogListeAsync(WebGisObjektart.Schacht, detailRef, "1");

        Assert.NotNull(liste);
        Assert.Contains(liste!, e => e.Key == "104" && e.Text == "Beton, Fertigteil");
        Assert.Contains("table=awk_abwasserknoten", gefragteUrl);
        Assert.Contains("refid=" + detailRef, gefragteUrl);
        Assert.Contains("filter=1", gefragteUrl);
        Assert.DoesNotContain("subtype=", gefragteUrl); // die Objektmasken haben keinen Subtyp wie AWZ_UNTERHALT
    }

    // ---------------- Serverfehler ist nicht Sitzungsfehler; Subtyp der Maske ----------------

    private const string FormFault = "{\"faultstring\":\"Could not load form's default form.\"}";

    [Fact]
    public async Task Serverfehler_ohne_sitzungsbezug_ist_kein_sitzungsfehler()
    {
        // 22.09.2026 im Programm: getControlValues der Schachtmaske antwortete so, und die ganze
        // Pruefung brach als «Sitzung abgelaufen» ab. Ein Formularfehler ist ein Antwortfehler.
        var h = new FakeHandler((req, body) => req.RequestUri!.ToString().Contains("getControlValues") ? FormFault : "{}");
        await Assert.ThrowsAsync<WebGisAntwortException>(
            () => Client(h).LeseKatalogListeAsync(WebGisObjektart.Schacht, "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728", "1"));
    }

    [Fact]
    public async Task LeseAsync_serverfehler_beim_layout_ist_ein_antwortfehler_kein_sitzungsfehler()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver")) return Suche(req, body);
            if (url.Contains("getLayoutDataCombined")) return FormFault;
            return "{}";
        });
        var ex = await Assert.ThrowsAsync<WebGisAntwortException>(
            () => Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));
        Assert.Contains("Could not load form's default form.", ex.Message);
    }

    private static string LayoutMitSubtypJson() =>
        "[{\"components\":[]}," +
        "{\"objectKeyValue\":\"g1\",\"subtype\":[{\"name\":\"typ\",\"value\":\"1\"}],\"components\":[" +
        "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102,\"keys\":[102,104],\"values\":[\"Z2\",\"Z4\"]}" +
        "]}]";

    [Fact]
    public async Task LeseAsync_liest_den_subtyp_der_maske()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver")) return Suche(req, body);
            if (url.Contains("getLayoutDataCombined")) return LayoutMitSubtypJson();
            return "{}";
        });

        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377");

        Assert.Equal("typ:1", stand!.Subtyp);
    }

    [Fact]
    public async Task LeseKatalogListeAsync_gibt_den_subtyp_der_maske_mit()
    {
        const string detailRef = "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728";
        string? gefragteUrl = null;
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getControlValues"))
            {
                gefragteUrl = url;
                return "{\"components\":[{\"values\":[\"Beton, Fertigteil\"],\"keys\":[104],\"refId\":\"" + detailRef + "\"}]}";
            }
            return "{}";
        });

        await Client(h).LeseKatalogListeAsync(WebGisObjektart.Schacht, detailRef, "1", subtyp: "typ:1");

        Assert.Contains("subtype=typ:1", gefragteUrl);
    }

    // ---------------- Suche: GlobalID auch bei grossgeschriebener Klammer-Kodierung ----------------

    [Fact]
    public async Task LeseAsync_findet_die_globalid_auch_wenn_der_server_die_klammern_gross_kodiert()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver"))
                return body.Contains("GET_RESULTS")
                    ? "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[{\"extapp0_target\":\"https://x/AttributeEditor/indexWebOffice.aspx?table=awk_haltung&ID=%7B" + Gid + "%7D\"}]}}}]}"
                    : SuggestionJson("525145-505377");
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            return "{}";
        });

        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377");

        Assert.NotNull(stand);
        Assert.Equal(Gid, stand!.GlobalId);
    }
}
