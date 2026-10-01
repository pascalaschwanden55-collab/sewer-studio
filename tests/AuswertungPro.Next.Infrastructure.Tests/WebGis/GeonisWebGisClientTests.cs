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

public sealed class GeonisWebGisClientTests
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
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public FakeHandler(Func<HttpRequestMessage, string, string> antwort) => _antwort = antwort;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var url = request.RequestUri!.ToString();
            if (url.Contains("saveData")) LetzterSaveBody = body;
            var json = _antwort(request, body);
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private const string ZustandRef = WebGisFeldkarte.HaltungZustandRef;
    private const string BemRef = WebGisFeldkarte.HaltungBemerkungRef;

    private static string LayoutJson() =>
        "[{\"components\":[]}," +
        "{\"components\":[" +
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

    private static GeonisWebGisClient Client(FakeHandler h)
        => new(new HttpClient(h), () => Zugang());

    [Fact]
    public async Task LeseAsync_loest_globalid_und_liest_felder()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver"))
                return body.Contains("GET_RESULTS")
                    ? ResultsJson("81D76B9E-2E83-40B2-B1F7-608BABE4E89A")
                    : SuggestionJson("525145-505377");
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            return "{}";
        });

        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377");

        Assert.NotNull(stand);
        Assert.Equal("81D76B9E-2E83-40B2-B1F7-608BABE4E89A", stand!.GlobalId);
        Assert.Equal("102", stand.Feld(ZustandRef));
        Assert.Equal("", stand.Feld(BemRef));
    }

    // Entscheid Pascal 23.09.2026: Ist die GlobalID einmal gespeichert, liest das Programm direkt
    // ueber sie — keine Namenssuche mehr. Der Name kommt dann aus der Maske selbst, damit der
    // Ablauf pruefen kann, ob das WebGIS-Objekt noch denselben Namen traegt.
    [Fact]
    public async Task LeseUeberGlobalIdAsync_liest_die_maske_ohne_suche_und_nimmt_den_namen_aus_der_maske()
    {
        var urls = new List<string>();
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            urls.Add(url);
            if (url.Contains("getLayoutDataCombined"))
                return "[{\"components\":[]},{\"components\":["
                    + "{\"refId\":\"" + WebGisFeldkarte.HaltungBezeichnungRef + "\",\"value\":\"80638-80631 \"},"
                    + "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102,\"keys\":[102,104],\"values\":[\"Z2\",\"Z4\"]}"
                    + "]}]";
            throw new InvalidOperationException("Keine Suche erwartet: " + url);
        });

        var stand = await Client(h).LeseUeberGlobalIdAsync(WebGisObjektart.Haltung, "31946755-E44D-4714-8DA6-91F56A6AC8FA");

        Assert.NotNull(stand);
        Assert.Equal("31946755-E44D-4714-8DA6-91F56A6AC8FA", stand!.GlobalId);
        Assert.Equal("80638-80631", stand.Bezeichnung);
        Assert.Equal("102", stand.Feld(ZustandRef));
        var aufruf = Assert.Single(urls);
        Assert.Contains("table=awk_haltung", aufruf);
        Assert.Contains("id=31946755-E44D-4714-8DA6-91F56A6AC8FA", aufruf);
    }

    [Fact]
    public async Task LeseUeberGlobalIdAsync_ohne_namensfeld_liefert_leeren_namen()
    {
        var h = new FakeHandler((_, _) => LayoutJson());

        var stand = await Client(h).LeseUeberGlobalIdAsync(WebGisObjektart.Schacht, "G1");

        Assert.NotNull(stand);
        Assert.Equal("", stand!.Bezeichnung); // fehlt der Name, sperrt der Planbau — nie geraten
    }

    [Fact]
    public async Task LeseUeberGlobalIdAsync_ohne_anmeldung_meldet_sitzungsfehler()
    {
        var h = new FakeHandler((_, _) => throw new InvalidOperationException("Keine Anfrage erwartet."));

        await Assert.ThrowsAsync<WebGisSitzungException>(() =>
            new GeonisWebGisClient(new HttpClient(h), () => null)
                .LeseUeberGlobalIdAsync(WebGisObjektart.Haltung, "G1"));
    }

    [Fact]
    public async Task LeseAsync_ohne_suchsession_meldet_sitzungsfehler_statt_kein_treffer()
    {
        var h = new FakeHandler((_, _) => throw new InvalidOperationException("Keine Anfrage erwartet."));
        var zugang = Zugang();
        var ohneSuchsession = new WebGisZugang
        {
            BasisUrl = zugang.BasisUrl, Projekt = zugang.Projekt, Datenquelle = zugang.Datenquelle,
            JSessionId = zugang.JSessionId, SynSessionId = null, SynLogin = zugang.SynLogin,
            SynGroups = zugang.SynGroups,
        };

        var ex = await Assert.ThrowsAsync<WebGisSitzungException>(() =>
            new GeonisWebGisClient(new HttpClient(h), () => ohneSuchsession)
                .LeseAsync(WebGisObjektart.Haltung, "525145-505377"));

        Assert.Contains("Such", ex.Message);
    }

    [Fact]
    public async Task LeseAsync_ohne_anmeldung_meldet_sitzungsfehler_statt_kein_treffer()
    {
        var h = new FakeHandler((_, _) => throw new InvalidOperationException("Keine Anfrage erwartet."));

        await Assert.ThrowsAsync<WebGisSitzungException>(() =>
            new GeonisWebGisClient(new HttpClient(h), () => null)
                .LeseAsync(WebGisObjektart.Haltung, "525145-505377"));
    }

    [Fact]
    public async Task LeseAsync_ungueltige_suchantwort_meldet_fehler_statt_kein_treffer()
    {
        var h = new FakeHandler((_, _) => "{}");

        var ex = await Assert.ThrowsAsync<WebGisSitzungException>(() =>
            Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));

        Assert.Contains("Such", ex.Message);
    }

    [Fact]
    public async Task LeseAsync_unberechtigte_suche_meldet_sitzungsfehler()
    {
        var h = new FakeHandler((_, _) => "") { StatusCode = HttpStatusCode.Unauthorized };

        var ex = await Assert.ThrowsAsync<WebGisSitzungException>(() =>
            Client(h).LeseAsync(WebGisObjektart.Haltung, "525145-505377"));

        Assert.Contains("Such", ex.Message);
    }

    [Fact]
    public async Task LeseAsync_mehrdeutig_gibt_null()
    {
        var h = new FakeHandler((req, body) =>
            "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[{\"record\":[{\"record\":[" +
            "{\"jsxtext\":\"H, a\",\"jsxid\":\"F1\"},{\"jsxtext\":\"H, b\",\"jsxid\":\"F2\"}" +
            "]}]}]}}}]}");
        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "H");
        Assert.Null(stand);
    }

    [Fact]
    public void Mehrere_ergebnis_links_liefern_keine_globalid()
    {
        using var dokument = System.Text.Json.JsonDocument.Parse(
            "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[" +
            "{\"extapp0_target\":\"https://x/?id=%7b81D76B9E-2E83-40B2-B1F7-608BABE4E89A%7d\"}," +
            "{\"extapp0_target\":\"https://x/?id=%7b31946755-E44D-4714-8DA6-91F56A6AC8FA%7d\"}" +
            "]}}}]}");

        Assert.Null(GeonisWebGisClient.GlobalIdAusResults(dokument.RootElement));
    }

    [Fact]
    public void Globalid_aus_editor_link_mit_sichtbaren_klammern()
    {
        using var dokument = System.Text.Json.JsonDocument.Parse(
            "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[" +
            "{\"extapp0_target\":\"https://x/AttributeEditor/?id={31946755-E44D-4714-8DA6-91F56A6AC8FA}\"}" +
            "]}}}]}");

        Assert.Equal("31946755-E44D-4714-8DA6-91F56A6AC8FA",
            GeonisWebGisClient.GlobalIdAusResults(dokument.RootElement));
    }

    [Fact]
    public async Task LeseAsync_gueltige_leere_suchliste_gibt_null()
    {
        var h = new FakeHandler((_, _) => "{\"RES\":[{\"RESULTS\":{\"data\":{\"anies\":[]}}}]}");

        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "nicht-vorhanden");

        Assert.Null(stand);
    }

    [Fact]
    public async Task SchreibeAsync_setzt_nur_geaenderte_komponente_als_keySelected()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            // Die echte Antwort des Servers (mitgeschnitten 21.09.2026); eine erfundene Form gilt nicht als Erfolg.
            if (url.Contains("saveData")) return "{\"newId\":null,\"file\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1",
            new Dictionary<string, string> { [ZustandRef] = "104" });

        Assert.True(res.Erfolg);
        Assert.NotNull(h.LetzterSaveBody);
        var payload = Uri.UnescapeDataString(h.LetzterSaveBody!);
        // Combo-Schluessel geht als Text in "value" (keySelected ignoriert der Server; value:null leert das Feld).
        Assert.Contains("\"value\":\"104\",\"refId\":\"" + ZustandRef + "\"", payload);
        Assert.DoesNotContain("\"value\":null", payload);
        Assert.DoesNotContain("\"keys\"", payload);
        // nur EINE Komponente (die Bemerkung nicht mitgeschrieben)
        Assert.DoesNotContain(BemRef, payload);
        // keine Geometrie im Payload
        Assert.DoesNotContain("geometry", payload);
    }

    // ---------------- Stufe 2: Sanierungsmassnahmen ----------------

    private const string ListeRef = "406ab302-0d93-eb1f-6824-e7ff7ff47fe5";

    private static string LayoutMitSanierungJson() =>
        "[{\"components\":[]}," +
        "{\"objectKeyValue\":\"g1\",\"components\":[" +
        "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102,\"keys\":[102,104],\"values\":[\"Z2\",\"Z4\"]}," +
        "{\"refId\":\"" + ListeRef + "\",\"values\":[[null,\"Renovierung\",\"Ausgeführt\",\"Schlauchverfahren\",\"3855a0d1-da52-4b6a-b416-2502672c947d\"]],\"value\":null}" +
        "]}]";

    private static string LeeresSanierungsobjektJson() =>
        "{\"objectKeyValue\":null,\"objectKeyField\":\"objectid\",\"subtype\":[{\"name\":\"art\",\"value\":\"4\"}]," +
        "\"dataReadonly\":false,\"geometry\":null,\"relation\":null,\"relationKeyField\":null,\"relationId\":null," +
        "\"components\":[" +
        "{\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.BezeichnungRef + "\",\"missingValue\":false}," +
        "{\"values\":[\"Reparatur\",\"Renovierung\"],\"keys\":[2,4],\"keySelected\":4,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.ArtRef + "\",\"missingValue\":false}," +
        "{\"values\":[\"Unbekannt\",\"Ausgeführt\"],\"keys\":[0,1],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.StatusRef + "\",\"missingValue\":false}," +
        "{\"values\":[\"Kurzrohrverfahren\",\"Schlauchverfahren\"],\"keys\":[13,27],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"missingValue\":false}," +
        "{\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.SanierungsjahrRef + "\",\"missingValue\":false}" +
        "]}";

    [Fact]
    public async Task LeseAsync_liest_vorhandene_sanierungsmassnahmen_aus_der_liste()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("synserver"))
                return body.Contains("GET_RESULTS") ? ResultsJson("67BA283D-24AE-4218-86E9-E82442A91D97") : SuggestionJson("80480-80478");
            if (url.Contains("getLayoutDataCombined")) return LayoutMitSanierungJson();
            return "{}";
        });

        var stand = await Client(h).LeseAsync(WebGisObjektart.Haltung, "80480-80478");

        Assert.NotNull(stand);
        var z = Assert.Single(stand!.Sanierungen);
        Assert.Equal("Renovierung", z.Art);
        Assert.Equal("Ausgeführt", z.Status);
        Assert.Equal("Schlauchverfahren", z.Verfahren);
        Assert.Equal("3855a0d1-da52-4b6a-b416-2502672c947d", z.GlobalId);
        Assert.False(stand.Felder.ContainsKey(ListeRef));
    }

    [Fact]
    public async Task LeseSanierungKatalogAsync_baut_katalog_aus_getEmptyData()
    {
        string? gefragteUrl = null;
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) { gefragteUrl = url; return LeeresSanierungsobjektJson(); }
            if (url.Contains("getControlValues"))
                return url.Contains("filter=2")
                    ? "{\"components\":[{\"values\":[\"Roboterverfahren\",\"Vermörtelung\"],\"keys\":[23,33],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"missingValue\":false}]}"
                    : "{\"components\":[{\"values\":[\"Kurzrohrverfahren\",\"Schlauchverfahren\"],\"keys\":[13,27],\"keySelected\":null,\"value\":null,\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\",\"missingValue\":false}]}";
            return "{}";
        });

        var k = await Client(h).LeseSanierungKatalogAsync(WebGisObjektart.Haltung, "67BA283D");

        Assert.NotNull(k);
        Assert.Equal("27", k!.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Schlauchverfahren"));
        Assert.Equal("4", k.Schluessel(WebGisSanierungFeldkarte.ArtRef, "Renovierung"));
        // abhaengige Verfahrensliste je Art (getControlValues filter=2 -> Vermoertelung)
        Assert.Equal("33", k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung", "2"));
        Assert.Null(k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung", "4"));
        Assert.Contains("table=AWZ_UNTERHALT", gefragteUrl);
        Assert.Contains("subtype=art:4", gefragteUrl);
        Assert.Contains("senderTable=awk_haltung", gefragteUrl);
        Assert.Contains("senderRelation=sew_awk_haltung_awz_unterhalt", gefragteUrl);
        Assert.Contains("senderId=67BA283D", gefragteUrl);
    }

    [Fact]
    public async Task ErstelleSanierungAsync_sendet_relation_zum_elternobjekt_und_liefert_neue_id()
    {
        string? saveUrl = null;
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeeresSanierungsobjektJson();
            if (url.Contains("saveData")) { saveUrl = url; return "{\"newId\":\"66921\",\"file\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}"; }
            return "{}";
        });

        var res = await Client(h).ErstelleSanierungAsync(
            WebGisObjektart.Haltung, "67BA283D-24AE-4218-86E9-E82442A91D97",
            new Dictionary<string, string>
            {
                [WebGisSanierungFeldkarte.StatusRef] = "1",
                [WebGisSanierungFeldkarte.VerfahrenRef] = "27",
                [WebGisSanierungFeldkarte.SanierungsjahrRef] = "2026-01-01T00:00:00.000Z",
            });

        Assert.True(res.Erfolg);
        Assert.Equal("66921", res.NeueId);
        Assert.Contains("table=AWZ_UNTERHALT", saveUrl);
        var payload = Uri.UnescapeDataString(h.LetzterSaveBody!);
        Assert.Contains("\"relation\":\"sew_awk_haltung_awz_unterhalt\"", payload);
        Assert.Contains("\"relationKeyField\":\"globalid\"", payload);
        Assert.Contains("\"relationId\":\"67BA283D-24AE-4218-86E9-E82442A91D97\"", payload);
        Assert.Contains("\"subtype\":[{\"name\":\"art\",\"value\":\"4\"}]", payload); // konstant wie im Browser
        Assert.Contains("\"value\":\"27\",\"refId\":\"" + WebGisSanierungFeldkarte.VerfahrenRef + "\"", payload);
        Assert.Contains("\"value\":\"2026-01-01T00:00:00.000Z\"", payload);
        // Vorgabe Art=4 der Maske bleibt erhalten, keine Kataloge im Payload
        Assert.Contains("\"value\":\"4\",\"refId\":\"" + WebGisSanierungFeldkarte.ArtRef + "\"", payload);
        Assert.DoesNotContain("\"keys\"", payload);
        Assert.DoesNotContain("geometry", payload);
    }

    [Fact]
    public async Task ErstelleSanierungAsync_meldet_isFailure_als_fehler()
    {
        var h = new FakeHandler((req, body) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getEmptyData")) return LeeresSanierungsobjektJson();
            if (url.Contains("saveData")) return "{\"newId\":null,\"message\":\"Regelverletzung\",\"isFailure\":true}";
            return "{}";
        });

        var res = await Client(h).ErstelleSanierungAsync(
            WebGisObjektart.Schacht, "G", new Dictionary<string, string> { [WebGisSanierungFeldkarte.StatusRef] = "1" });

        Assert.False(res.Erfolg);
        Assert.Contains("Regelverletzung", res.Fehler);
    }

    [Fact]
    public async Task LeseMassnahmeAsync_liest_die_sanierungsmaske_ueber_ihre_globalid()
    {
        string? gelesen = null;
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (!url.Contains("getLayoutDataCombined")) return "{}";
            gelesen = url;
            return "[{\"components\":[]}," + LeeresSanierungsobjektJson() + "]";
        });

        var m = await Client(h).LeseMassnahmeAsync("3855a0d1-da52-4b6a-b416-2502672c947d");

        Assert.NotNull(m);
        Assert.Contains("table=" + WebGisSanierungFeldkarte.Tabelle, gelesen);
        Assert.Contains("id=3855a0d1-da52-4b6a-b416-2502672c947d", gelesen);
        Assert.Equal("4", m!.Feld(WebGisSanierungFeldkarte.ArtRef));
        Assert.Contains(("4", "Renovierung"), m.Kataloge[WebGisSanierungFeldkarte.ArtRef]);
    }

    // Entscheid Pascal 23.09.2026: Eigentum, Betreiber, Laenge, Baujahr, GlobalID und Objekt-ID werden
    // im WebGIS nie ueberschrieben. Der Sendeteil ist die letzte Tuer — auch er sendet sie nie.
    private const string EigentuemerRef = "fadff6f2-c674-9327-36d8-b2ac79b704cd";

    private static string LayoutMitSchutzfeldernJson(string baujahr) =>
        "[{\"components\":[]},{\"components\":[" +
        "{\"refId\":\"" + ZustandRef + "\",\"keySelected\":102,\"keys\":[102,104],\"values\":[\"Z2\",\"Z4\"]}," +
        "{\"refId\":\"" + EigentuemerRef + "\",\"keySelected\":\"58d1c876\",\"keys\":[\"58d1c876\"],\"values\":[\"Kanton Uri\"]}," +
        "{\"refId\":\"" + WebGisFeldkarte.HaltungBaujahrRef + "\",\"value\":" + baujahr + "}" +
        "]}]";

    [Theory]
    [InlineData(EigentuemerRef, "x", "null")]
    [InlineData(WebGisFeldkarte.HaltungBaujahrRef, "1970", "\"1963\"")]
    public async Task SchreibeAsync_sendet_ein_geschuetztes_feld_nie(string refId, string neu, string baujahrImWebGis)
    {
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutMitSchutzfeldernJson(baujahrImWebGis);
            if (url.Contains("saveData")) return "{\"newId\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104", [refId] = neu });

        Assert.False(res.Erfolg);
        Assert.Null(h.LetzterSaveBody); // gar nichts gesendet, auch nicht der Zustand
    }

    [Fact]
    public async Task SchreibeAsync_fuellt_ein_leeres_baujahr()
    {
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutMitSchutzfeldernJson("null");
            if (url.Contains("saveData")) return "{\"newId\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [WebGisFeldkarte.HaltungBaujahrRef] = "1970" });

        Assert.True(res.Erfolg, res.Fehler);
        Assert.Contains("\"value\":\"1970\"", Uri.UnescapeDataString(h.LetzterSaveBody!));
    }

    [Fact]
    public async Task SchreibeAsync_sendet_nichts_wenn_der_letzte_stand_vom_erwarteten_abweicht()
    {
        // Audit A04 (23.09.2026): Die Fremdaenderung kommt genau zwischen der Pruefung im Ablauf und dem
        // letzten Lesen im Client. Auch dieses letzte Lesen muss gegen den bestaetigten Stand pruefen.
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson(); // Zustand jetzt 102
            if (url.Contains("saveData")) return "{\"newId\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });
        var erwartet = new Dictionary<string, string?>(StringComparer.Ordinal) { [ZustandRef] = "101", [BemRef] = "" };

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" }, erwarteterStand: erwartet);

        Assert.False(res.Erfolg);
        Assert.Contains("seit der Prüfung geändert", res.Fehler);
        Assert.Null(h.LetzterSaveBody);
    }

    [Fact]
    public async Task SchreibeAsync_schreibt_wenn_der_letzte_stand_dem_erwarteten_entspricht()
    {
        var h = new FakeHandler((req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("getLayoutDataCombined")) return LayoutJson();
            if (url.Contains("saveData")) return "{\"newId\":null,\"message\":\"Das Objekt wurde gespeichert.\",\"isFailure\":false}";
            return "{}";
        });
        var erwartet = new Dictionary<string, string?>(StringComparer.Ordinal) { [ZustandRef] = "102", [BemRef] = "" };

        var res = await Client(h).SchreibeAsync(
            WebGisObjektart.Haltung, "g1", new Dictionary<string, string> { [ZustandRef] = "104" }, erwarteterStand: erwartet);

        Assert.True(res.Erfolg, res.Fehler);
        Assert.NotNull(h.LetzterSaveBody);
    }
}
