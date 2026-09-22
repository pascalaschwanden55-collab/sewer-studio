using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Spricht den GEONIS-Attributeditor (WebOffice) ueber seine interne Schnittstelle an,
/// genau wie der Browser: Suche (synserver, GET_QUERY_FULL_TEXT -> GET_RESULTS) fuer die
/// GlobalID, getLayoutDataCombined zum Lesen, saveData zum Schreiben genau der
/// geaenderten Komponenten.
///
/// Der HttpClient wird injiziert (im Test ein Fake-Handler). Die Sitzung liefert
/// <see cref="_zugang"/> — sie muss angemeldet sein (siehe GeonisWebOfficeLogin).
/// </summary>
public sealed partial class GeonisWebGisClient : IGeonisWebGisClient
{
    private readonly HttpClient _http;
    private readonly Func<WebGisZugang?> _zugang;

    public GeonisWebGisClient(HttpClient http, Func<WebGisZugang?> zugang)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _zugang = zugang ?? throw new ArgumentNullException(nameof(zugang));
    }

    public async Task<WebGisLesestand?> LeseAsync(
        WebGisObjektart art, string bezeichnung, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bezeichnung)) return null;
        if (_zugang() is null) return null;
        var globalId = await LoeseGlobalIdAsync(art, bezeichnung.Trim(), ct).ConfigureAwait(false);
        if (globalId is null) return null;
        return await LiesFelderAsync(art, bezeichnung.Trim(), globalId, ct).ConfigureAwait(false);
    }

    public async Task<WebGisSchreibErgebnis> SchreibeAsync(
        WebGisObjektart art, string globalId,
        IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
    {
        if (felder.Count == 0) return WebGisSchreibErgebnis.Ok();
        var z = _zugang();
        if (z is null) return WebGisSchreibErgebnis.Fehlgeschlagen("Nicht am WebGIS angemeldet.");
        var tabelle = WebGisFeldkarte.Tabelle(art);

        // 1) Vollen Layout+Datenstand holen (fuer die Komponenten-Objekte).
        var layout = await LiesLayoutAsync(tabelle, globalId, ct).ConfigureAwait(false);
        if (layout is null) return WebGisSchreibErgebnis.Fehlgeschlagen("Layout/Daten nicht lesbar.");

        var data = layout[1];
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("components", out var comps)
            || comps.ValueKind != JsonValueKind.Array)
            return WebGisSchreibErgebnis.Fehlgeschlagen("Datenstand ohne components.");

        // 2) Nur die geaenderten Komponenten uebernehmen, neuen Wert setzen.
        var modified = new List<Dictionary<string, object?>>();
        foreach (var comp in comps.EnumerateArray())
        {
            if (!comp.TryGetProperty("refId", out var refEl)) continue;
            var refId = refEl.GetString() ?? "";
            if (!felder.TryGetValue(refId, out var neu)) continue;

            // Genau wie der Browser: {value, refId, missingValue}. Auch bei Combos geht der
            // Schluessel als Text in "value" — "keySelected" wird vom Server IGNORIERT und ein
            // value:null LEERT das Feld (so geschehen am 21.09.2026, 44 Objekte; danach repariert).
            modified.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = neu, ["refId"] = refId, ["missingValue"] = false,
            });
        }
        if (modified.Count != felder.Count)
        {
            // Ein geplantes Feld ohne Komponente heisst: falsche refId oder anderes Maskenlayout.
            // Dann wird NICHTS geschrieben — sonst gaelte ein halb geschriebenes Objekt als Erfolg
            // (Pruefung 22.09.2026).
            var fehlend = new List<string>();
            foreach (var refId in felder.Keys)
                if (!modified.Exists(m => (string?)m["refId"] == refId)) fehlend.Add(refId);
            return WebGisSchreibErgebnis.Fehlgeschlagen(
                "Geplante Felder ohne Komponente in der WebGIS-Maske: " + string.Join(", ", fehlend)
                + " — Objekt nicht geschrieben.");
        }

        // 3) Kopf des Datenobjekts uebernehmen, Geometrie weglassen, components ersetzen.
        var payload = new Dictionary<string, object?>();
        foreach (var p in data.EnumerateObject())
        {
            if (p.NameEquals("components") || p.NameEquals("geometry") || p.NameEquals("_debug")) continue;
            payload[p.Name] = JsonElementZuObjekt(p.Value);
        }
        payload["components"] = modified;

        var json = JsonSerializer.Serialize(payload);
        var body = "jsonobject=" + Uri.EscapeDataString(json) + "&" + z.AuthQuery();
        var url = z.EditorBasis + "saveData?project=" + z.Datenquelle + "&datasource=" + z.Datenquelle
                + "&table=" + tabelle + "&lang=de&f=pjson&ts=" + Ts();

        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return WebGisSchreibErgebnis.Fehlgeschlagen($"HTTP {(int)resp.StatusCode}: {Kurz(text)}");
        return AntwortAuswerten(text);
    }

    // --- Suche: Bezeichnung -> GlobalID (eindeutig) ---
    private async Task<string?> LoeseGlobalIdAsync(WebGisObjektart art, string bezeichnung, CancellationToken ct)
    {
        var z = _zugang();
        if (z is null) return null;
        var idx = WebGisFeldkarte.Suchindex(art);
        var sugg = await SynPostAsync(
            $"request_id={Ts()}|serveraction=GET_QUERY_FULL_TEXT|subaction=get_results_suggestion|"
            + $"session_id={z.SynSessionId}|is_fts=true|subindex_id={idx}|value={bezeichnung}", ct)
            .ConfigureAwait(false);
        if (sugg is null) return null;

        var fids = new List<string>();
        foreach (var r in SuggestionsFinden(sugg.Value))
        {
            var text = r.TryGetProperty("jsxtext", out var t) ? (t.GetString() ?? "") : "";
            var name = text.Split(',')[0].Trim();
            if (string.Equals(name, bezeichnung, StringComparison.Ordinal)
                && r.TryGetProperty("jsxid", out var idEl))
                fids.Add(idEl.GetString() ?? "");
        }
        if (fids.Count != 1) return null;

        var res = await SynPostAsync(
            $"request_id={Ts()}|serveraction=GET_RESULTS|subaction=get_results_suggested|"
            + $"session_id={z.SynSessionId}|is_fts=true|selection_type=new|fidset={fids[0]}", ct)
            .ConfigureAwait(false);
        if (res is null) return null;
        return GlobalIdAusResults(res.Value);
    }

    // --- Feldwerte lesen (je refId) ---
    private async Task<WebGisLesestand?> LiesFelderAsync(
        WebGisObjektart art, string bezeichnung, string globalId, CancellationToken ct)
    {
        var layout = await LiesLayoutAsync(WebGisFeldkarte.Tabelle(art), globalId, ct).ConfigureAwait(false);
        if (layout is null) return null;
        var data = layout[1];
        var felder = new Dictionary<string, string?>(StringComparer.Ordinal);
        var kataloge = new Dictionary<string, List<(string Key, string Text)>>(StringComparer.Ordinal);
        var sanierungen = new List<WebGisSanierungZeile>();
        var listeRef = WebGisSanierungFeldkarte.ListeRef(art);
        if (data.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
        {
            foreach (var comp in comps.EnumerateArray())
            {
                if (!comp.TryGetProperty("refId", out var refEl)) continue;
                var refId = refEl.GetString() ?? "";
                if (refId == listeRef)
                {
                    sanierungen.AddRange(SanierungsZeilen(comp));
                    continue;
                }
                if (comp.TryGetProperty("keySelected", out var ks) && ks.ValueKind != JsonValueKind.Null)
                    felder[refId] = ks.ToString();
                else if (comp.TryGetProperty("value", out var v))
                    felder[refId] = v.ValueKind == JsonValueKind.Null ? null : v.ToString();

                // Auswahllisten mitnehmen: nur so laesst sich ein Handwert-Klartext in den
                // Schluessel der Maske uebersetzen, ohne Codes fest zu verdrahten.
                var liste = KatalogAus(comp);
                if (liste is not null) kataloge[refId] = liste;
            }
        }
        return new WebGisLesestand
        {
            GlobalId = globalId, Bezeichnung = bezeichnung, Felder = felder,
            Sanierungen = sanierungen, Kataloge = kataloge, Subtyp = SubtypAus(data),
        };
    }

    private async Task<JsonElement[]?> LiesLayoutAsync(string tabelle, string globalId, CancellationToken ct)
    {
        var z = _zugang();
        if (z is null) return null;
        var url = z.EditorBasis + "getLayoutDataCombined?project=" + z.Datenquelle + "&datasource=" + z.Datenquelle
                + "&table=" + tabelle + "&lang=de&f=pjson&id=" + globalId + "&ts=" + Ts() + "&" + z.AuthQuery();
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = LiesJsonOderSitzungsfehler(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
            return null;
        return new[] { doc.RootElement[0].Clone(), doc.RootElement[1].Clone() };
    }

    private async Task<JsonElement?> SynPostAsync(string query, CancellationToken ct)
    {
        var z = _zugang();
        if (z is null) return null;
        var body = "client=corejs&query=" + Uri.EscapeDataString(query);
        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var resp = await _http.PostAsync(z.SynServerUrl, content, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = LiesJsonOderSitzungsfehler(text);
        return doc.RootElement.Clone();
    }

    // --- JSON-Helfer ---
    internal static IEnumerable<JsonElement> SuggestionsFinden(JsonElement root)
    {
        // RES[0].RESULTS.data.anies[0].record[0].record  (Liste der Vorschlaege)
        if (!root.TryGetProperty("RES", out var res) || res.ValueKind != JsonValueKind.Array || res.GetArrayLength() == 0)
            yield break;
        var results = res[0].TryGetProperty("RESULTS", out var r) ? r : default;
        if (results.ValueKind != JsonValueKind.Object) yield break;
        if (!results.TryGetProperty("data", out var data) || !data.TryGetProperty("anies", out var anies)
            || anies.ValueKind != JsonValueKind.Array || anies.GetArrayLength() == 0) yield break;
        var a0 = anies[0];
        if (!a0.TryGetProperty("record", out var rec1) || rec1.ValueKind != JsonValueKind.Array || rec1.GetArrayLength() == 0) yield break;
        if (!rec1[0].TryGetProperty("record", out var rec2) || rec2.ValueKind != JsonValueKind.Array) yield break;
        foreach (var r2 in rec2.EnumerateArray()) yield return r2;
    }

    internal static string? GlobalIdAusResults(JsonElement root)
    {
        if (!root.TryGetProperty("RES", out var res) || res.ValueKind != JsonValueKind.Array || res.GetArrayLength() == 0)
            return null;
        if (!res[0].TryGetProperty("RESULTS", out var r) || !r.TryGetProperty("data", out var data)
            || !data.TryGetProperty("anies", out var anies) || anies.ValueKind != JsonValueKind.Array
            || anies.GetArrayLength() == 0) return null;
        var rec = anies[0];
        if (!rec.TryGetProperty("extapp0_target", out var tgt)) return null;
        var url = tgt.GetString() ?? "";
        // %7b/%7d sind die kodierten Klammern; ob der Server sie klein oder gross schreibt, ist nicht garantiert.
        var m = System.Text.RegularExpressions.Regex.Match(
            url, "id=%7b([0-9A-Fa-f-]+)%7d", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>keys/values einer Combo-Komponente als Liste (Schluessel als Text, Klartext).</summary>
    internal static List<(string Key, string Text)>? KatalogAus(JsonElement comp)
    {
        if (!comp.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array) return null;
        if (!comp.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) return null;
        var n = Math.Min(keys.GetArrayLength(), values.GetArrayLength());
        var liste = new List<(string, string)>(n);
        for (var i = 0; i < n; i++)
            liste.Add((keys[i].ToString(), values[i].ValueKind == JsonValueKind.Null ? "" : values[i].ToString()));
        return liste;
    }

    private static Dictionary<string, object?> JsonZuDict(JsonElement obj)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject()) d[p.Name] = JsonElementZuObjekt(p.Value);
        return d;
    }

    private static object? JsonElementZuObjekt(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Array => ArrayZu(e),
        JsonValueKind.Object => JsonZuDict(e),
        _ => e.ToString(),
    };

    private static List<object?> ArrayZu(JsonElement e)
    {
        var l = new List<object?>();
        foreach (var x in e.EnumerateArray()) l.Add(JsonElementZuObjekt(x));
        return l;
    }

    private static string Ts() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
        + Random.Shared.Next(100, 999).ToString(CultureInfo.InvariantCulture);

    private static string Kurz(string s, int max = 300) => s.Length <= max ? s : s[..max];

    /// <summary>
    /// Jede Antwort der Erweiterung wird hier gelesen: Ein faultstring ist die abgelaufene
    /// Sitzung, und etwas, das kein JSON ist (ADFS-Anmeldeseite, Proxy-Seite, leerer Rumpf),
    /// ebenso. Beides darf nie als «nicht gefunden» oder «Lesefehler» je Objekt durchgehen —
    /// sonst stehen 52 gesperrte Objekte da statt einer Meldung «neu anmelden» (Pruefung 22.09.2026).
    /// </summary>
    private static JsonDocument LiesJsonOderSitzungsfehler(string text)
    {
        PruefeSitzung(text);
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            throw new WebGisSitzungException(
                "Das WebGIS hat keine lesbare Antwort geliefert (Anmeldeseite oder Serverfehler) — bitte abmelden und neu anmelden. Antwort: "
                + Kurz(text.Trim(), 120));
        }
    }

    /// <summary>
    /// Die Erweiterung antwortet auch bei abgelaufener Sitzung mit HTTP 200, aber einem
    /// {faultstring: "Das empfangene Authentifizierungs-Token ist ... abgelaufen"} —
    /// beobachtet 21.09.2026. Das darf nie als "nicht gefunden" oder "gespeichert" durchgehen.
    /// </summary>
    private static void PruefeSitzung(string text)
    {
        if (!text.Contains("\"faultstring\"", StringComparison.OrdinalIgnoreCase)) return;
        var fault = Faultstring(text);
        if (IstSitzungsfehler(fault)) throw new WebGisSitzungException(fault);
        // Ein Formular- oder Regelfehler betrifft genau diesen Aufruf, nicht die Sitzung
        // («Could not load form's default form.», 22.09.2026) — Objekt/Feld melden, weiterlaufen.
        throw new WebGisAntwortException("WebGIS meldet: " + fault);
    }

    /// <summary>Nur Token-/Anmeldefehler beenden den ganzen Lauf.</summary>
    internal static bool IstSitzungsfehler(string fault)
    {
        var f = fault.ToLowerInvariant();
        return f.Contains("token") || f.Contains("abgelaufen") || f.Contains("expired")
            || f.Contains("authenti") || f.Contains("session") || f.Contains("sitzung")
            || f.Contains("login") || f.Contains("anmeld") || f.Contains("unauthori");
    }

    /// <summary>Subtyp der Maske aus dem Datenobjekt als «name:wert» (z.B. «art:4»); null ohne Subtyp.</summary>
    internal static string? SubtypAus(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;
        if (!data.TryGetProperty("subtype", out var st) || st.ValueKind != JsonValueKind.Array || st.GetArrayLength() == 0) return null;
        var s0 = st[0];
        if (s0.ValueKind != JsonValueKind.Object) return null;
        if (!s0.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) return null;
        if (!s0.TryGetProperty("value", out var v) || v.ValueKind == JsonValueKind.Null) return null;
        var name = n.GetString();
        var wert = v.ToString();
        return string.IsNullOrEmpty(name) || wert.Length == 0 ? null : name + ":" + wert;
    }

    internal static string Faultstring(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("faultstring", out var f) && f.ValueKind == JsonValueKind.String)
                return f.GetString() ?? "WebGIS-Sitzung ungueltig.";
        }
        catch (JsonException) { }
        return "WebGIS-Sitzung ungueltig.";
    }
}
