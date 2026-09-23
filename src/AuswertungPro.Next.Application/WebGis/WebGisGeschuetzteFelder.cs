using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Letzte Sperre vor dem Senden ins WebGIS (Entscheid Pascal 23.09.2026): Eigentum, Betreiber,
/// Haltungslaenge, Baujahr, GlobalID und Objekt-ID werden in der WebGIS-Datenbank nie
/// ueberschrieben, dazu die Bezeichnung, an der das Objekt erkannt wird.
///
/// Zwei Stufen: Die ausdruecklich geschuetzten Felder sind mit Namen genannt (klare Meldung), und
/// freigegeben ist ueberhaupt nur, was der Export planen kann — Zustand, Sanierungsbedarf,
/// Bemerkung, das leere Baujahr (Haltung und Schacht) und die Felder der <see cref="WebGisHandwertKarte"/>.
/// Alles andere, auch eine Kennung, die niemand als geschuetzt kennt, geht nie hinaus.
///
/// refIds aus der Masken-Inventur v2 (Buerglen, 21.09.2026). Die GlobalID ist keine Komponente der
/// Maske, sondern der Schluessel des Objekts; sie faellt unter «nicht freigegeben».
/// </summary>
public static class WebGisGeschuetzteFelder
{
    private static readonly IReadOnlyDictionary<string, string> HaltungNie = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WebGisFeldkarte.HaltungBezeichnungRef] = "Bezeichnung",
        [WebGisFeldkarte.HaltungObjectIdRef] = "OBJECTID",
        [WebGisFeldkarte.HaltungLaengeGeomRef] = "Länge geom./eff.",
        [WebGisFeldkarte.HaltungLaengeRohrRef] = "Rohr-/Haltungslänge",
        [WebGisFeldkarte.HaltungEigentuemerRef] = "Eigentümer",
        [WebGisFeldkarte.HaltungBetreiberRef] = "Betreiber",
        ["e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a"] = "Baujahr/Ersatzjahr",
    };

    private static readonly IReadOnlyDictionary<string, string> SchachtNie = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WebGisFeldkarte.SchachtBezeichnungRef] = "Bezeichnung",
        [WebGisFeldkarte.SchachtObjectIdRef] = "OBJECTID",
        [WebGisFeldkarte.SchachtEigentuemerRef] = "Eigentümer",
        [WebGisFeldkarte.SchachtBetreiberRef] = "Betreiber",
    };

    private static readonly IReadOnlyDictionary<string, string> HaltungLeer = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WebGisFeldkarte.HaltungBaujahrRef] = "Baujahr",
    };

    private static readonly IReadOnlyDictionary<string, string> SchachtLeer = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WebGisFeldkarte.SchachtBaujahrRef] = "Baujahr",
    };

    /// <summary>Felder, die das Programm im WebGIS nie aendert (refId -> Anzeigename).</summary>
    public static IReadOnlyDictionary<string, string> NieSchreiben(WebGisObjektart art)
        => art == WebGisObjektart.Haltung ? HaltungNie : SchachtNie;

    /// <summary>Felder, die nur gefuellt werden duerfen, solange sie im WebGIS leer sind (Baujahr).</summary>
    public static IReadOnlyDictionary<string, string> NurWennLeer(WebGisObjektart art)
        => art == WebGisObjektart.Haltung ? HaltungLeer : SchachtLeer;

    /// <summary>Darf der Export dieses Feld ueberhaupt senden? Nur was er planen kann.</summary>
    public static bool IstFreigegeben(WebGisObjektart art, string refId)
    {
        if (NieSchreiben(art).ContainsKey(refId)) return false;
        if (refId == WebGisFeldkarte.ZustandRef(art) || refId == WebGisFeldkarte.SanierungsbedarfRef(art)
            || refId == WebGisFeldkarte.BemerkungRef(art)) return true;
        if (refId == WebGisFeldkarte.BaujahrRef(art)) return true; // nur in ein leeres Feld (NurWennLeer)
        foreach (var f in WebGisHandwertKarte.Felder)
            if (f.Objektart == art && (f.RefId == refId || f.HauptRefId == refId)) return true;
        return false;
    }

    /// <summary>
    /// Verstoesse der zu sendenden Felder gegen die Schutzregeln; leer heisst: darf gesendet werden.
    /// <paramref name="aktuellerWert"/> liefert den Wert, der JETZT im WebGIS steht (frisch gelesen).
    /// Ein Verstoss sperrt das ganze Objekt — nie ein halbes Objekt senden.
    /// </summary>
    public static IReadOnlyList<string> Verstoesse(
        WebGisObjektart art, IReadOnlyDictionary<string, string> felder, Func<string, string?> aktuellerWert)
    {
        ArgumentNullException.ThrowIfNull(felder);
        ArgumentNullException.ThrowIfNull(aktuellerWert);
        var verstoesse = new List<string>();
        foreach (var refId in felder.Keys)
        {
            if (NieSchreiben(art).TryGetValue(refId, out var name))
            {
                verstoesse.Add($"{name} wird im WebGIS nie geändert — Objekt nicht geschrieben.");
                continue;
            }
            if (NurWennLeer(art).TryGetValue(refId, out var leerName))
            {
                var jetzt = (aktuellerWert(refId) ?? string.Empty).Trim();
                if (jetzt.Length > 0)
                    verstoesse.Add($"{leerName} steht im WebGIS schon («{jetzt}») und wird nie überschrieben — Objekt nicht geschrieben.");
                continue;
            }
            if (!IstFreigegeben(art, refId))
                verstoesse.Add($"Feld {refId} ist für das Schreiben ins WebGIS nicht freigegeben — Objekt nicht geschrieben.");
        }
        return verstoesse;
    }
}
