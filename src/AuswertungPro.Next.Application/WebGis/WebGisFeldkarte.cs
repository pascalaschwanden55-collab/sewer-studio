using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Die feste Feldzuordnung SewerStudio -> GEONIS-Attributeditor, maschinell aus den
/// echten Masken awk_haltung / awk_abwasserknoten am 21.09.2026 erhoben
/// (siehe Projektablage __WebGIS_Export/Feldzuordnung_SewerStudio_WebGIS_v2.md).
///
/// Nur der bewusst freigegebene Ausschnitt fuer den Sanierungs-Export ist hier
/// hinterlegt: Zustand, Sanierungsbedarf, Bemerkung, Baujahr (Haltung). Die refIds
/// sind Objekt-unabhaengig (sie stammen aus dem Layout, nicht aus den Daten).
///
/// Reine Werte-Logik, kein Zustand, kein Dateizugriff.
/// </summary>
public static class WebGisFeldkarte
{
    // --- Haltung (Tabelle awk_haltung) ---
    public const string HaltungZustandRef = "1b817d9e-26d9-cc56-df8d-d52c23509841";
    public const string HaltungSanierungsbedarfRef = "2b200c69-4a70-bae0-64e7-8d2e3aae6871";
    public const string HaltungBemerkungRef = "5027c330-73e2-4674-9cbc-8efed43d655e";
    public const string HaltungBaujahrRef = "72b0bc78-b7b7-8dd3-bc64-e41cf40aa5ca";
    public const string HaltungLaengeGeomRef = "e5b5b42c-8970-3ebf-8bea-5705ca4be842"; // NUR LESEN
    public const string HaltungLaengeRohrRef = "7e439b47-81e0-b7d6-b05b-575178d48c3c"; // NIE SCHREIBEN

    // --- Schacht (Tabelle awk_abwasserknoten) ---
    public const string SchachtZustandRef = "1e0208ec-0373-12d3-725d-55e374fbfd51";
    public const string SchachtSanierungsbedarfRef = "ae898ff7-8b6d-a170-36cf-ce904e9b6639";
    public const string SchachtBemerkungRef = "979ffb47-1ac0-da09-a398-02f1347727f5";
    /// <summary>Baujahr Schacht: EditBox ohne Titel (2. Feld des Paars Material/Baujahr); live geprueft 21.09.2026 (505377=2018, 80475=1963).</summary>
    public const string SchachtBaujahrRef = "e35e99dc-3754-dd97-296d-8bb8b70c61c3";

    public static string BaujahrRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBaujahrRef : SchachtBaujahrRef;

    /// <summary>WebGIS-Tabellenname je Objektart.</summary>
    public static string Tabelle(WebGisObjektart art) => art switch
    {
        WebGisObjektart.Haltung => "awk_haltung",
        WebGisObjektart.Schacht => "awk_abwasserknoten",
        _ => throw new ArgumentOutOfRangeException(nameof(art))
    };

    /// <summary>Suchindex (Full-Text) je Objektart fuer die GlobalID-Aufloesung.</summary>
    public static string Suchindex(WebGisObjektart art) => art switch
    {
        WebGisObjektart.Haltung => "awu_abw_edit_aw_haltung",
        WebGisObjektart.Schacht => "awu_abw_edit_aw_schacht",
        _ => throw new ArgumentOutOfRangeException(nameof(art))
    };

    public static string ZustandRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungZustandRef : SchachtZustandRef;

    public static string SanierungsbedarfRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungSanierungsbedarfRef : SchachtSanierungsbedarfRef;

    public static string BemerkungRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBemerkungRef : SchachtBemerkungRef;

    /// <summary>Sanierungsbedarf-Code fuer "saniert".</summary>
    public const int SanierungsbedarfSaniert = 106;

    private static readonly IReadOnlyDictionary<string, int> ZustandsklasseNachCode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["0"] = 100, // Nicht mehr funktionstuechtig (Z0)
            ["1"] = 101, // Starke Maengel (Z1)
            ["2"] = 102, // Mittlere Maengel (Z2)
            ["3"] = 103, // Leichte Maengel (Z3)
            ["4"] = 104, // Keine Maengel (Z4)
        };

    private static readonly IReadOnlyDictionary<int, string> ZustandKlartext =
        new Dictionary<int, string>
        {
            [100] = "Nicht mehr funktionstuechtig (Z0)",
            [101] = "Starke Maengel (Z1)",
            [102] = "Mittlere Maengel (Z2)",
            [103] = "Leichte Maengel (Z3)",
            [104] = "Keine Maengel (Z4)",
        };

    /// <summary>
    /// Uebersetzt die SewerStudio-Zustandsklasse (0..4) in den WebGIS-Code.
    /// Liefert null bei leerem oder unbekanntem Wert (dann keine Aenderung).
    /// </summary>
    public static int? ZustandCode(string? zustandsklasse)
    {
        var s = (zustandsklasse ?? string.Empty).Trim();
        return ZustandsklasseNachCode.TryGetValue(s, out var code) ? code : (int?)null;
    }

    public static string ZustandText(int code) =>
        ZustandKlartext.TryGetValue(code, out var t) ? t : code.ToString();

    public static string SanierungsbedarfText(int code) =>
        code == SanierungsbedarfSaniert ? "Saniert" : code.ToString();
}
