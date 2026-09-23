using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Vergleicht zwei gelesene Staende desselben WebGIS-Objekts ueber ALLE Felder der Maske
/// (Wunsch Pascal 23.09.2026: «im WebGIS gibt es Aenderungsdaten; weicht das vom Gelesenen ab,
/// muss das geprueft werden»). Das Feld «Geändert am (UTC)» ist eine Komponente der Maske und
/// zaehlt damit mit; seine refId ist nicht erhoben, deshalb wird nicht nur dieses eine Feld,
/// sondern der ganze Stand verglichen — jede fremde Bearbeitung faellt so auf.
/// Reine Rechnung.
/// </summary>
public static class WebGisStandVergleich
{
    /// <summary>Kurzer, stabiler Fingerabdruck eines Stands (Reihenfolge egal, Leerraum am Rand egal).</summary>
    public static string Fingerabdruck(IReadOnlyDictionary<string, string?>? felder)
    {
        if (felder is null) return string.Empty;
        var text = string.Join("\n", felder
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key + "=" + Norm(kv.Value)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>refIds, deren Wert sich zwischen <paramref name="vorher"/> und <paramref name="jetzt"/> unterscheidet.</summary>
    public static IReadOnlyList<string> Abweichungen(
        IReadOnlyDictionary<string, string?> vorher, IReadOnlyDictionary<string, string?> jetzt)
    {
        ArgumentNullException.ThrowIfNull(vorher);
        ArgumentNullException.ThrowIfNull(jetzt);
        return vorher.Keys.Union(jetzt.Keys, StringComparer.Ordinal)
            .Where(k => Norm(Wert(vorher, k)) != Norm(Wert(jetzt, k)))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Lesbarer Name einer refId fuer Meldungen; unbekannte Felder mit ihrer Kurzkennung.</summary>
    public static string Anzeigename(WebGisObjektart art, string refId)
    {
        if (refId == WebGisFeldkarte.ZustandRef(art)) return "Zustand";
        if (refId == WebGisFeldkarte.SanierungsbedarfRef(art)) return "Sanierungsbedarf";
        if (refId == WebGisFeldkarte.BemerkungRef(art)) return "Bemerkung";
        if (WebGisGeschuetzteFelder.NieSchreiben(art).TryGetValue(refId, out var nie)) return nie;
        if (WebGisGeschuetzteFelder.NurWennLeer(art).TryGetValue(refId, out var leer)) return leer;
        foreach (var f in WebGisHandwertKarte.Felder)
            if (f.Objektart == art && (f.RefId == refId || f.HauptRefId == refId)) return f.Anzeige;
        return "Feld " + (refId.Length > 8 ? refId[..8] : refId);
    }

    private static string? Wert(IReadOnlyDictionary<string, string?> d, string k) => d.TryGetValue(k, out var v) ? v : null;

    private static string Norm(string? s) => (s ?? string.Empty).Trim();
}
