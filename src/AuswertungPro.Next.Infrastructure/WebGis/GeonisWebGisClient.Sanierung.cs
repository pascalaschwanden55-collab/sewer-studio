using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Stufe 2: Sanierungsmassnahmen (AWZ_UNTERHALT, art=4) am Elternobjekt anlegen.
/// Nachgebaut aus dem im Browser mitgeschnittenen Anlege-Aufruf (21.09.2026):
/// getEmptyData (liefert leere Komponenten samt Combo-Katalogen) -> saveData mit
/// relation/relationKeyField/relationId im Kopf; Antwort {newId, isFailure, message}.
/// </summary>
public sealed partial class GeonisWebGisClient
{
    public async Task<WebGisSanierungKatalog?> LeseSanierungKatalogAsync(
        WebGisObjektart art, string elternGlobalId, CancellationToken ct = default)
    {
        var leer = await LiesLeeresSanierungsobjektAsync(art, elternGlobalId, ct).ConfigureAwait(false);
        if (leer is null) return null;
        var data = leer.Value;
        if (!data.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array) return null;

        var katalog = new WebGisSanierungKatalog();
        foreach (var comp in comps.EnumerateArray())
        {
            if (!comp.TryGetProperty("refId", out var refEl)) continue;
            if (!comp.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array) continue;
            if (!comp.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) continue;
            var n = Math.Min(keys.GetArrayLength(), values.GetArrayLength());
            var liste = new List<(string, string)>(n);
            for (var i = 0; i < n; i++)
                liste.Add((keys[i].ToString(), values[i].ValueKind == JsonValueKind.Null ? "" : values[i].ToString()));
            katalog.Setze(refEl.GetString() ?? "", liste);
        }

        // Verfahren haengt von der Art ab: je Art-Schluessel die gefilterte Liste holen.
        // Fehlt auch nur eine, ist der ganze Katalog unbrauchbar: Dieselbe Nummer bedeutet je
        // Art etwas anderes, und ein Rueckfall auf die ungefilterte Liste wuerde ein falsches
        // Verfahren anlegen. Lieber alle Massnahmen sperren (Pruefung 22.09.2026).
        var subtyp = WebGisSanierungFeldkarte.SubtypFeld + ":" + WebGisSanierungFeldkarte.SubtypWert;
        foreach (var (artKey, _) in katalog.Eintraege(WebGisSanierungFeldkarte.ArtRef))
        {
            var gefiltert = await LiesGefilterteWerteAsync(
                WebGisSanierungFeldkarte.Tabelle, subtyp, WebGisSanierungFeldkarte.VerfahrenRef, artKey, ct).ConfigureAwait(false);
            if (gefiltert is null) return null;
            katalog.Setze(WebGisSanierungFeldkarte.VerfahrenRef, gefiltert, artKey);
        }
        return katalog;
    }

    /// <summary>
    /// Abhaengige Liste eines Combo-Felds der Objektmaske, z.B. die Material-Details der
    /// Gruppe «Beton», wenn die Maske gerade eine andere Gruppe zeigt. Der Aufruf folgt dem
    /// fuer AWZ_UNTERHALT mitgeschnittenen Muster, nur ohne Subtyp; traegt die Antwort die
    /// refId nicht, kommt null zurueck — das Feld bleibt dann gemeldet, nie geraten.
    /// </summary>
    public async Task<IReadOnlyList<(string Key, string Text)>?> LeseKatalogListeAsync(
        WebGisObjektart art, string refId, string filter, string? subtyp = null, CancellationToken ct = default)
        => await LiesGefilterteWerteAsync(WebGisFeldkarte.Tabelle(art), subtyp, refId, filter, ct).ConfigureAwait(false);

    /// <summary>
    /// getControlValues: abhaengige Combo-Liste (refid) fuer einen Filterwert. Mit
    /// <paramref name="subtyp"/> fuer AWZ_UNTERHALT (art:4), ohne fuer die Objektmasken.
    /// Null, wenn die Antwort die refId nicht traegt.
    /// </summary>
    private async Task<List<(string, string)>?> LiesGefilterteWerteAsync(
        string tabelle, string? subtyp, string refId, string filter, CancellationToken ct)
    {
        var z = _zugang();
        if (z is null) return null;
        var url = z.EditorBasis + "getControlValues?project=" + z.Datenquelle + "&datasource=" + z.Datenquelle
                + "&table=" + tabelle + "&lang=de&f=pjson&ts=" + Ts()
                + (subtyp is null ? "" : "&subtype=" + subtyp)
                + "&refid=" + refId + "&filter=" + Uri.EscapeDataString(filter) + "&" + z.AuthQuery();
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = LiesJsonOderSitzungsfehler(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
        if (!doc.RootElement.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array) return null;
        foreach (var comp in comps.EnumerateArray())
        {
            if (!comp.TryGetProperty("refId", out var r) || r.GetString() != refId) continue;
            return KatalogAus(comp);
        }
        return null;
    }

    public async Task<WebGisSchreibErgebnis> ErstelleSanierungAsync(
        WebGisObjektart art, string elternGlobalId,
        IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
    {
        if (felder.Count == 0) return WebGisSchreibErgebnis.Fehlgeschlagen("Keine Felder zum Anlegen.");
        var z = _zugang();
        if (z is null) return WebGisSchreibErgebnis.Fehlgeschlagen("Nicht am WebGIS angemeldet.");

        var leer = await LiesLeeresSanierungsobjektAsync(art, elternGlobalId, ct).ConfigureAwait(false);
        if (leer is null) return WebGisSchreibErgebnis.Fehlgeschlagen("Leeres Sanierungsobjekt nicht lesbar.");
        var data = leer.Value;
        if (!data.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array)
            return WebGisSchreibErgebnis.Fehlgeschlagen("Leeres Sanierungsobjekt ohne components.");

        // Alle Komponenten wie der Browser: {value, refId, missingValue}; Combo-Wert = Schluessel als Text.
        var komponenten = new List<Dictionary<string, object?>>();
        var gesetzt = 0;
        foreach (var comp in comps.EnumerateArray())
        {
            if (!comp.TryGetProperty("refId", out var refEl)) continue;
            var refId = refEl.GetString() ?? "";
            object? wert = null;
            if (felder.TryGetValue(refId, out var neu)) { wert = neu; gesetzt++; }
            else if (comp.TryGetProperty("keySelected", out var ks) && ks.ValueKind != JsonValueKind.Null)
                wert = ks.ToString(); // Vorgabe der Maske (z.B. Art=4) beibehalten
            komponenten.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = wert, ["refId"] = refId, ["missingValue"] = false,
            });
        }
        if (gesetzt == 0)
            return WebGisSchreibErgebnis.Fehlgeschlagen("Keine passende Komponente in der Sanierungsmaske gefunden.");

        // Subtyp bleibt wie im Browser konstant art:4 (Layout-Vorgabe); GEONIS uebernimmt den
        // gespeicherten Subtyp aus dem Feld Art (live geprueft 21.09.2026: Art=2 -> subtype art=2).
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["components"] = komponenten,
            ["objectKeyValue"] = null,
            ["objectKeyField"] = "objectid",
            ["subtype"] = new[] { new Dictionary<string, object?> { ["name"] = WebGisSanierungFeldkarte.SubtypFeld, ["value"] = WebGisSanierungFeldkarte.SubtypWert } },
            ["dataReadonly"] = false,
            ["relation"] = WebGisSanierungFeldkarte.Relation(art),
            ["relationKeyField"] = WebGisSanierungFeldkarte.RelationSchluesselfeld,
            ["relationId"] = elternGlobalId,
        };

        var json = JsonSerializer.Serialize(payload);
        var body = "jsonobject=" + Uri.EscapeDataString(json) + "&" + z.AuthQuery();
        var url = z.EditorBasis + "saveData?project=" + z.Datenquelle + "&datasource=" + z.Datenquelle
                + "&table=" + WebGisSanierungFeldkarte.Tabelle + "&lang=de&f=pjson&ts=" + Ts();

        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return WebGisSchreibErgebnis.Fehlgeschlagen($"HTTP {(int)resp.StatusCode}: {Kurz(text)}");
        return AntwortAuswerten(text);
    }

    /// <summary>getEmptyData fuer AWZ_UNTERHALT art=4 mit Sender-Kontext (Elternobjekt).</summary>
    private async Task<JsonElement?> LiesLeeresSanierungsobjektAsync(
        WebGisObjektart art, string elternGlobalId, CancellationToken ct)
    {
        var z = _zugang();
        if (z is null) return null;
        var url = z.EditorBasis + "getEmptyData?project=" + z.Datenquelle + "&datasource=" + z.Datenquelle
                + "&table=" + WebGisSanierungFeldkarte.Tabelle
                + "&subtype=" + WebGisSanierungFeldkarte.SubtypFeld + ":" + WebGisSanierungFeldkarte.SubtypWert
                + "&lang=de&f=pjson"
                + "&senderTable=" + WebGisFeldkarte.Tabelle(art)
                + "&senderRefId=" + WebGisSanierungFeldkarte.ListeRef(art)
                + "&senderRelation=" + WebGisSanierungFeldkarte.Relation(art)
                + "&senderId=" + elternGlobalId
                + "&ts=" + Ts() + "&" + z.AuthQuery();
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = LiesJsonOderSitzungsfehler(text);
        return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
    }

    /// <summary>
    /// Antwort von saveData: {newId, message, isFailure} — oder ein faultstring (Sitzung abgelaufen).
    /// Erfolg gibt es NUR mit einem Erfolgsnachweis des Servers (isFailure:false, newId oder
    /// message). Alles andere — kein JSON, kein Objekt, leeres Objekt — heisst «nicht als
    /// geschrieben gewertet»: Ein falsches OK im Log ist schlimmer als ein Fehlschlag, den man
    /// nachpruefen kann (Lehre vom 21.09.2026, hier zu Ende gedacht).
    /// </summary>
    internal static WebGisSchreibErgebnis AntwortAuswerten(string text)
    {
        if (text.Contains("\"faultstring\"", StringComparison.OrdinalIgnoreCase))
        {
            var fault = Faultstring(text);
            return IstSitzungsfehler(fault)
                ? WebGisSchreibErgebnis.Fehlgeschlagen("WebGIS-Sitzung: " + fault + " — neu anmelden.")
                : WebGisSchreibErgebnis.Fehlgeschlagen("WebGIS meldet Fehler: " + fault);
        }
        if (text.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase))
            return WebGisSchreibErgebnis.Fehlgeschlagen("WebGIS meldet Fehler: " + Kurz(text));

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return WebGisSchreibErgebnis.Fehlgeschlagen(
                "Unerwartete Antwort des WebGIS (kein JSON) — nicht als geschrieben gewertet: " + Kurz(text.Trim(), 120));
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return WebGisSchreibErgebnis.Fehlgeschlagen(
                    "Unerwartete Antwort des WebGIS (kein Objekt) — nicht als geschrieben gewertet: " + Kurz(text.Trim(), 120));

            var hatIsFailure = root.TryGetProperty("isFailure", out var f);
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            if ((hatIsFailure && f.ValueKind == JsonValueKind.True) || root.TryGetProperty("error", out _))
                return WebGisSchreibErgebnis.Fehlgeschlagen("WebGIS meldet Fehler: " + (message ?? Kurz(text)));

            var hatNewId = root.TryGetProperty("newId", out var id);
            if (!hatIsFailure && !hatNewId && message is null)
                return WebGisSchreibErgebnis.Fehlgeschlagen(
                    "Antwort ohne Erfolgsnachweis (weder isFailure noch newId noch message) — nicht als geschrieben gewertet: " + Kurz(text.Trim(), 120));

            var neueId = hatNewId && id.ValueKind != JsonValueKind.Null ? id.ToString() : null;
            return WebGisSchreibErgebnis.Ok(neueId);
        }
    }

    /// <summary>Zeilen der Liste "Sanierungsmassnahmen": [Beginn, Art, Status, Verfahren, GlobalId].</summary>
    internal static IEnumerable<WebGisSanierungZeile> SanierungsZeilen(JsonElement listComp)
    {
        if (!listComp.TryGetProperty("values", out var rows) || rows.ValueKind != JsonValueKind.Array) yield break;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 5) continue;
            yield return new WebGisSanierungZeile
            {
                Beginn = Zelle(row[0]), Art = Zelle(row[1]), Status = Zelle(row[2]), Verfahren = Zelle(row[3]), GlobalId = Zelle(row[4]),
            };
        }
    }

    private static string? Zelle(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.ToString();
}
