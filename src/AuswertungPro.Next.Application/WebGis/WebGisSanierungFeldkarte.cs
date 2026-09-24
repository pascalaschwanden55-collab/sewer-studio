using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Feste Feldzuordnung Sanierungs-Objektakte (SewerStudio) -> Maske
/// "Sanierungsmassnahme" im WebGIS (Tabelle AWZ_UNTERHALT, Subtyp art=4).
/// Maschinell erhoben am 21.09.2026 (Anlege-Aufruf im Browser mitgeschnitten,
/// siehe Projektablage __WebGIS_Export/Feldzuordnung_SewerStudio_WebGIS_v2.md).
///
/// Die Combo-Schluessel werden NICHT hier fest verdrahtet, sondern zur Laufzeit aus
/// dem WebGIS-Katalog (getEmptyData: keys/values je Combo) per Klartext aufgeloest —
/// SewerStudio-LokalerEintrag und WebGIS-Code sind verschiedene Nummernkreise.
///
/// Reine Werte-Logik, kein Zustand, kein Dateizugriff.
/// </summary>
public static class WebGisSanierungFeldkarte
{
    /// <summary>WebGIS-Tabelle der Sanierungsmassnahme.</summary>
    public const string Tabelle = "AWZ_UNTERHALT";
    /// <summary>Subtyp-Feld und -Wert: art=4 (Layout "Sanierungsmassnahme").</summary>
    public const string SubtypFeld = "art";
    public const string SubtypWert = "4";
    /// <summary>Schluesselfeld der Relation zum Elternobjekt (Haltung/Schacht).</summary>
    public const string RelationSchluesselfeld = "globalid";

    // --- refIds der Maske "Sanierungsmassnahme" (layout-, nicht objektbezogen) ---
    public const string BezeichnungRef = "7f25415d-f1b3-5064-5004-69e99676deda";   // EditBox
    public const string ArtRef = "a8835985-0736-5de2-261b-76a0848ae9de";           // Combo
    public const string StatusRef = "35c9f3c7-13e1-b6eb-d5d9-7683b1184b1b";        // Combo
    public const string VerfahrenRef = "8573dac2-8c36-00fe-44a7-91a29b5a7f13";     // Combo
    public const string UmfangRef = "d89b27b2-dd75-dd2c-291a-dfa72bb1ce57";        // Combo
    public const string SanierungsjahrRef = "e1b9c707-31c4-260f-36db-e9fe7e4c889e"; // DateBox (TT.MM.JJJJ)
    public const string ProfiltypRef = "e4b24ff9-87f9-34db-c750-8521630591b2";     // Combo
    public const string FabrikatRef = "47502f6b-fa14-8869-9489-3344cc5b3397";      // Combo
    public const string HerstellerRef = "01b89043-9b30-f067-f7f6-55621c94b56d";    // Combo

    // --- Akte-Schluessel in SewerStudio (ObjektAkte.Werte) ---
    public const string AkteName = "sanierung.s_name";
    public const string AkteArt = "sanierung.s_art";
    public const string AkteStatus = "sanierung.s_status";
    public const string AkteVerfahren = "sanierung.s_procedure";
    public const string AkteUmfang = "sanierung.s_extent";
    public const string AkteJahr = "sanierung.s_year";
    public const string AkteProfiltyp = "sanierung.s_profile";
    public const string AkteFabrikat = "sanierung.s_product";
    public const string AkteHersteller = "sanierung.s_manufacturer";

    /// <summary>Combo-Felder: Akte-Schluessel -> (refId, Anzeigename). Reihenfolge wie in der Maske.</summary>
    public static readonly IReadOnlyList<(string AkteKey, string RefId, string Feld)> ComboFelder = new[]
    {
        (AkteArt, ArtRef, "Art"),
        (AkteStatus, StatusRef, "Status"),
        (AkteVerfahren, VerfahrenRef, "Verfahren"),
        (AkteUmfang, UmfangRef, "Umfang"),
        (AkteProfiltyp, ProfiltypRef, "Profiltyp"),
        (AkteFabrikat, FabrikatRef, "Fabrikat"),
        (AkteHersteller, HerstellerRef, "Hersteller"),
    };

    /// <summary>Relation Elternobjekt -> AWZ_UNTERHALT je Objektart.</summary>
    public static string Relation(WebGisObjektart art) => art switch
    {
        WebGisObjektart.Haltung => "sew_awk_haltung_awz_unterhalt",
        WebGisObjektart.Schacht => "sew_awk_abwasserknoten_awz_unterhalt",
        _ => throw new ArgumentOutOfRangeException(nameof(art))
    };

    /// <summary>refId der Liste "Sanierungsmassnahmen" (GListBox) in der Elternmaske.</summary>
    public static string ListeRef(WebGisObjektart art) => art switch
    {
        WebGisObjektart.Haltung => "406ab302-0d93-eb1f-6824-e7ff7ff47fe5",
        WebGisObjektart.Schacht => "3b90f4b4-8047-8107-12bc-10b45de60377",
        _ => throw new ArgumentOutOfRangeException(nameof(art))
    };

    /// <summary>
    /// Sanierungsjahr: SewerStudio fuehrt nur das Jahr, das WebGIS ein Datum.
    /// Regel: 1. Januar des Jahres, als UTC-Mitternacht wie der Browser es sendet.
    /// Null bei leerem/ungueltigem Jahr.
    /// </summary>
    public static string? JahrAlsDatum(string? jahr)
    {
        var s = (jahr ?? string.Empty).Trim();
        if (s.Length != 4 || !int.TryParse(s, out var j) || j < 1900 || j > 2100) return null;
        return $"{j:0000}-01-01T00:00:00.000Z";
    }

    /// <summary>Jahr aus einem Datum des WebGIS: «01.01.2026», ISO, Millisekunden seit 1970 oder nur «2026».</summary>
    public static string? JahrAusDatum(string? wert)
    {
        var t = (wert ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        if (t.Length >= 11 && long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.Year.ToString(CultureInfo.InvariantCulture);
        var m = Regex.Match(t, @"(?<!\d)(19\d\d|20\d\d|2100)(?!\d)");
        return m.Success ? m.Value : null;
    }
}
