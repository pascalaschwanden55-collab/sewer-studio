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
///
/// Jede refId-Konstante traegt ein Etikett «Status: live geprueft (…)» oder «Status: offen (…)» (Wartbarkeitsaudit
/// 30.09.2026, WG-A). Quelle sind ausschliesslich docs/architektur/webgis.md und die bisherigen Kommentare.
/// Jede refId steht im Quelltext genau einmal (Waechter <c>WebGisKennungenTests</c>); die Schacht-Aktenkarte
/// <see cref="WebGisImportAktenfelder"/> fuehrt ihre Inventur-refIds bewusst selbst.
/// </summary>
public static class WebGisFeldkarte
{
    // --- Haltung (Tabelle awk_haltung) ---
    /// <summary>Zustand Haltung. Status: live geprueft (Lauf 21.09.2026, Haltung 525145-505377: Z4).</summary>
    public const string HaltungZustandRef = "1b817d9e-26d9-cc56-df8d-d52c23509841";
    /// <summary>Sanierungsbedarf Haltung (2. Combo des Paars). Status: live geprueft (Lauf 21.09.2026, Haltung 525145-505377: Saniert).</summary>
    public const string HaltungSanierungsbedarfRef = "2b200c69-4a70-bae0-64e7-8d2e3aae6871";
    /// <summary>Bemerkung Haltung. Status: live geprueft (Lauf 21.09.2026, Haltung 525145-505377).</summary>
    public const string HaltungBemerkungRef = "5027c330-73e2-4674-9cbc-8efed43d655e";
    /// <summary>Baujahr Haltung. Status: offen (Masken-Erhebung 21.09.2026; kein Live-Beleg in webgis.md).</summary>
    public const string HaltungBaujahrRef = "72b0bc78-b7b7-8dd3-bc64-e41cf40aa5ca";
    /// <summary>
    /// Baujahr/Ersatzjahr Haltung — wird im WebGIS nie geschrieben (<see cref="WebGisGeschuetzteFelder"/>).
    /// Status: offen (Masken-Inventur v2, Buerglen 21.09.2026; kein Live-Beleg in webgis.md).
    /// </summary>
    public const string HaltungBaujahrErsatzjahrRef = "e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a";
    /// <summary>Laenge geometrisch Haltung. NUR LESEN. Status: offen (Masken-Erhebung 21.09.2026; kein Live-Beleg in webgis.md).</summary>
    public const string HaltungLaengeGeomRef = "e5b5b42c-8970-3ebf-8bea-5705ca4be842"; // NUR LESEN
    /// <summary>Rohr-/Haltungslaenge. NIE SCHREIBEN. Status: offen (Masken-Erhebung 21.09.2026; kein Live-Beleg in webgis.md).</summary>
    public const string HaltungLaengeRohrRef = "7e439b47-81e0-b7d6-b05b-575178d48c3c"; // NIE SCHREIBEN

    /// <summary>
    /// Breite [mm] der Haltung (Handwerte «Lichte_Breite_mm» und «DN_mm»; beim Holen die DN, wenn Breite = Hoehe).
    /// Status: live geprueft (21.09.2026, Haltung 80480-80478, sichtbar 300/300 — Breite und Hoehe dort gleich).
    /// </summary>
    public const string HaltungBreiteRef = "902695a4-5f44-e910-b2da-471c17085822";
    /// <summary>
    /// Hoehe [mm] der Haltung (Handwert «Lichte_Hoehe_mm»; beim Holen nur der Vergleich mit der Breite fuer die DN).
    /// Status: offen (webgis.md 22.09.2026: Breite/Hoehe der Haltung OFFEN, die Inventur v2 nennt diese refId einmal
    /// Breite, einmal Hoehe — an einem Eiprofil klaeren; am 21.09.2026 unter den drei erschlossenen, als FALSCH
    /// erkannten refIds genannt). Wert unveraendert.
    /// </summary>
    public const string HaltungHoeheRef = "d06f8d1f-8a09-1b22-4380-088a7ee42507";

    // --- Schacht (Tabelle awk_abwasserknoten) ---
    /// <summary>Zustand Schacht. Status: offen (Masken-Erhebung 21.09.2026; Live-Schreiben in webgis.md nur an der Haltung belegt).</summary>
    public const string SchachtZustandRef = "1e0208ec-0373-12d3-725d-55e374fbfd51";
    /// <summary>Sanierungsbedarf Schacht (2. Combo des Paars). Status: offen (Masken-Erhebung 21.09.2026; Live-Schreiben in webgis.md nur an der Haltung belegt).</summary>
    public const string SchachtSanierungsbedarfRef = "ae898ff7-8b6d-a170-36cf-ce904e9b6639";
    /// <summary>Bemerkung Schacht. Status: offen (Masken-Erhebung 21.09.2026; Live-Schreiben in webgis.md nur an der Haltung belegt).</summary>
    public const string SchachtBemerkungRef = "979ffb47-1ac0-da09-a398-02f1347727f5";
    /// <summary>Baujahr Schacht: EditBox ohne Titel (2. Feld des Paars Material/Baujahr). Status: live geprueft (21.09.2026, 505377=2018, 80475=1963).</summary>
    public const string SchachtBaujahrRef = "e35e99dc-3754-dd97-296d-8bb8b70c61c3";

    public static string BaujahrRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBaujahrRef : SchachtBaujahrRef;

    // --- Bezeichnung (EditBox «Bezeichnung», nur lesen) ---
    // Aus der maschinellen Masken-Inventur vom 21.09.2026 (Feldzuordnung v2, Haltung/Schacht je
    // «Bezeichnung | EditBox»). Gebraucht beim Lesen ueber die gespeicherte GlobalID: Nur so sieht
    // der Ablauf, ob das WebGIS-Objekt noch denselben Namen traegt. Live noch nicht gegen den
    // sichtbaren Wert geprueft — deshalb fail-closed: fehlt der Wert oder weicht er ab, wird das
    // Objekt gesperrt, nie geschrieben (Pascal 23.09.2026).
    /// <summary>Bezeichnung Haltung. Status: offen (Inventur v2, live noch nicht gegen den sichtbaren Wert geprueft).</summary>
    public const string HaltungBezeichnungRef = "e2bf0b38-f8fe-0a23-3f19-3cd2395b8d92";
    /// <summary>Bezeichnung Schacht. Status: offen (Inventur v2, live noch nicht gegen den sichtbaren Wert geprueft).</summary>
    public const string SchachtBezeichnungRef = "302da059-fda3-b684-3f61-3ce293bea795";

    public static string BezeichnungRef(WebGisObjektart art) =>
        art == WebGisObjektart.Haltung ? HaltungBezeichnungRef : SchachtBezeichnungRef;

    // --- Eigentuemer / Betreiber / OBJECTID (ComboBox bzw. EditBox, aus der Inventur v2, LIVE NICHT GEPRUEFT) ---
    // Nur lesen: Das Holen uebernimmt sie rein informativ in LEERE Felder (Entscheid Pascal 23.09.2026) und
    // prueft dabei, dass die Liste der Komponente wirklich Organisationen fuehrt. Geschrieben werden sie nie
    // (WebGisGeschuetzteFelder).
    /// <summary>Eigentuemer Haltung. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur mit Organisationsliste «Bund»).</summary>
    public const string HaltungEigentuemerRef = "fadff6f2-c674-9327-36d8-b2ac79b704cd";
    /// <summary>Betreiber Haltung. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur mit Organisationsliste «Bund»).</summary>
    public const string HaltungBetreiberRef = "bd6d1330-f106-9eb3-c079-22a2accd845c";
    /// <summary>OBJECTID Haltung. Status: offen (Inventur v2, live nicht geprueft).</summary>
    public const string HaltungObjectIdRef = "8410243c-beab-5ecb-b4e7-bc7907b9ee31";
    /// <summary>Eigentuemer Schacht. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur mit Organisationsliste «Bund»).</summary>
    public const string SchachtEigentuemerRef = "e2987817-9bdd-2cef-4617-729126d465a1";
    /// <summary>Betreiber Schacht. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur mit Organisationsliste «Bund»).</summary>
    public const string SchachtBetreiberRef = "1187d930-1cdb-d29b-2065-499d273dbeba";
    /// <summary>OBJECTID Schacht. Status: offen (Inventur v2, live nicht geprueft).</summary>
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
    /// <summary>Typ AA Haltung. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur, wenn die Liste PAA und SAA fuehrt).</summary>
    public const string HaltungTypAaRef = "65e83cb4-0e0f-ee6c-47c6-6efad172c3b1";
    /// <summary>Typ AA Schacht. Status: offen (Inventur v2, live nicht geprueft; zaehlt nur, wenn die Liste PAA und SAA fuehrt).</summary>
    public const string SchachtTypAaRef = "8d17a0bc-4472-d776-82f7-58ba57adf676";
    /// <summary>Praefix der hydraulischen Funktion des Schachts. Status: offen (Inventur v2 nennt nur acht Zeichen, live nicht geprueft).</summary>
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
