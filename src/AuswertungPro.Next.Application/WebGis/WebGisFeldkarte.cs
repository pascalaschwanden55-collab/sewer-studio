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

    // --- Bezeichnung (EditBox «Bezeichnung», nur lesen) ---
    // Aus der maschinellen Masken-Inventur vom 21.09.2026 (Feldzuordnung v2, Haltung/Schacht je
    // «Bezeichnung | EditBox»). Gebraucht beim Lesen ueber die gespeicherte GlobalID: Nur so sieht
    // der Ablauf, ob das WebGIS-Objekt noch denselben Namen traegt. Live noch nicht gegen den
    // sichtbaren Wert geprueft — deshalb fail-closed: fehlt der Wert oder weicht er ab, wird das
    // Objekt gesperrt, nie geschrieben (Pascal 23.09.2026).
    public const string HaltungBezeichnungRef = "e2bf0b38-f8fe-0a23-3f19-3cd2395b8d92";
    public const string SchachtBezeichnungRef = "302da059-fda3-b684-3f61-3ce293bea795";

    public static string BezeichnungRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBezeichnungRef : SchachtBezeichnungRef;

    // --- Eigentuemer / Betreiber / OBJECTID (ComboBox bzw. EditBox, aus der Inventur v2, LIVE NICHT GEPRUEFT) ---
    // Nur lesen: Das Holen uebernimmt sie rein informativ in LEERE Felder (Entscheid Pascal 23.09.2026) und
    // prueft dabei, dass die Liste der Komponente wirklich Organisationen fuehrt. Geschrieben werden sie nie
    // (WebGisGeschuetzteFelder).
    public const string HaltungEigentuemerRef = "fadff6f2-c674-9327-36d8-b2ac79b704cd";
    public const string HaltungBetreiberRef = "bd6d1330-f106-9eb3-c079-22a2accd845c";
    public const string HaltungObjectIdRef = "8410243c-beab-5ecb-b4e7-bc7907b9ee31";
    public const string SchachtEigentuemerRef = "e2987817-9bdd-2cef-4617-729126d465a1";
    public const string SchachtBetreiberRef = "1187d930-1cdb-d29b-2065-499d273dbeba";
    public const string SchachtObjectIdRef = "9bc2e78d-3a34-e836-5db6-357202362d20";

    public static string EigentuemerRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungEigentuemerRef : SchachtEigentuemerRef;

    public static string BetreiberRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBetreiberRef : SchachtBetreiberRef;

    // --- Typ AA (PAA/SAA) und hydraulische Funktion des Schachts (Inventur v2, LIVE NICHT GEPRUEFT) ---
    // Nur lesen (Holen, 24.09.2026). Ob eine Komponente wirklich Typ AA ist, prueft das Holen an ihrer Liste:
    // PAA und SAA muessen darin stehen. Die hydraulische Funktion des Schachts nennt die Inventur nur mit den
    // ersten acht Zeichen; gesucht wird die EINE Komponente mit diesem Praefix, deren Liste die hydraulischen
    // Funktionen fuehrt (Freispiegelleitung). Sonst nichts, mit Hinweis.
    public const string HaltungTypAaRef = "65e83cb4-0e0f-ee6c-47c6-6efad172c3b1";
    public const string SchachtTypAaRef = "8d17a0bc-4472-d776-82f7-58ba57adf676";
    public const string SchachtFunktionHydraulischPraefix = "0e5eab11-";

    public static string TypAaRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungTypAaRef : SchachtTypAaRef;

    /// <summary>
    /// Ein Schluessel, den jede Organisationsliste der Masken fuehrt («Bund», in Haltung und Schacht belegt).
    /// Fehlt er in der Liste einer Komponente, ist es nicht das Eigentuemer-/Betreiberfeld (refId falsch).
    /// </summary>
    public const string OrganisationBundKey = "df1f763b-7f01-4d4d-a22c-14476c7a3a9b";

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
