using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>Eine Faerberegel: welcher Zellwert bekommt welche Farbe.</summary>
/// <param name="Wert">Der Zellinhalt, auf den die Regel greift.</param>
/// <param name="Farbe">Vollstaendiger ARGB-Wert, wie Excel ihn erwartet.</param>
public readonly record struct ExcelFarbregel(string Wert, string Farbe);

/// <summary>
/// Der lesbare Laufzeitvertrag fuer die Bedeutungsfarben der Berichte. Der
/// Vorlagenbauer fuehrt dieselben Werte in <c>tools/ExcelVorlagenBauer/stil.py</c>;
/// ein Vorlagentreuetest vergleicht beide Seiten, damit sie nicht still auseinanderlaufen.
///
/// Vorher lagen sie in zwei binaeren Vorlagendateien und waren auseinandergelaufen:
/// Zustandsklasse 3 war bei den Haltungen AEB135, bei den Schaechten A5A832 - gleiche
/// Bedeutung, zwei Toene. Solche Abweichungen faellt in einer .xlsx niemandem auf.
/// </summary>
public static class ExcelReportStyle
{
    /// <summary>
    /// Zustandsklasse 0 (schlechteste) bis 4 (beste). Die Ampel laeuft bewusst
    /// rot - orange - gelb - oliv - gruen; oliv ist die Zwischenstufe vor gruen.
    /// </summary>
    public static IReadOnlyList<ExcelFarbregel> Zustandsklassen { get; } = new[]
    {
        new ExcelFarbregel("0", "FFFF0000"),
        new ExcelFarbregel("1", "FFFF6600"),
        new ExcelFarbregel("2", "FFFFFF00"),
        new ExcelFarbregel("3", "FFAEB135"),
        new ExcelFarbregel("4", "FF92D050")
    };

    /// <summary>Eigentuemer, damit die Zustaendigkeit auf einen Blick sichtbar ist.</summary>
    public static IReadOnlyList<ExcelFarbregel> Eigentuemer { get; } = new[]
    {
        new ExcelFarbregel("Kanton", "FFFFFF00"),
        new ExcelFarbregel("Bund", "FFFF8000"),
        new ExcelFarbregel("AWU", "FF548235"),
        new ExcelFarbregel("Gemeinde", "FF00B0F0"),
        new ExcelFarbregel("Privat", "FFFF0000")
    };

    /// <summary>
    /// Ergebnis der Haltungspruefung. SewerStudio kennt eine rechnerische
    /// Wertefamilie und die historisch gepflegte Dichtheitspruefung. Beide bleiben
    /// als Originaltext erhalten und verwenden nur dieselbe Ampelbedeutung.
    /// </summary>
    public static IReadOnlyList<ExcelFarbregel> Pruefungsresultate { get; } = new[]
    {
        new ExcelFarbregel("i.O.", "FF92D050"),
        new ExcelFarbregel("beobachten", "FFFFFF00"),
        new ExcelFarbregel("Sanierungsbedarf", "FFFF0000"),
        new ExcelFarbregel("Prüfung bestanden", "FF92D050"),
        new ExcelFarbregel("Prüfung knapp nicht bestanden", "FFFFFF00"),
        new ExcelFarbregel("Prüfung nicht bestanden (grob undicht)", "FFFF0000"),
        new ExcelFarbregel("Pruefung bestanden", "FF92D050"),
        new ExcelFarbregel("Pruefung knapp nicht bestanden", "FFFFFF00"),
        new ExcelFarbregel("Pruefung nicht bestanden (grob undicht)", "FFFF0000"),
        new ExcelFarbregel("Keine", "FFE7E6E6")
    };

    /// <summary>
    /// Bearbeitungsstand der Sanierung. Das Gruen ist bewusst ein anderes als bei
    /// Zustandsklasse 4: dort geht es um den Zustand des Bauwerks, hier um den Stand
    /// der Arbeit.
    /// </summary>
    public static IReadOnlyList<ExcelFarbregel> Status { get; } = new[]
    {
        new ExcelFarbregel("offen", "FFFF0000"),
        new ExcelFarbregel("abgeschlossen", "FF00B050")
    };

    // --- Grundgeruest ---------------------------------------------------------

    /// <summary>
    /// Titelbalken ueber der Tabelle. Gruen wie bisher (in den alten Vorlagen als
    /// Themenfarbe accent6 hinterlegt, aufgeloest 70AD47).
    /// </summary>
    public const string TitelHintergrund = "FF70AD47";
    public const string TitelSchrift = "FFFFFFFF";

    /// <summary>Kopfzeile der Tabelle - blau wie bisher (accent1, 4472C4).</summary>
    public const string KopfHintergrund = "FF4472C4";
    public const string KopfSchrift = "FFFFFFFF";

    /// <summary>Rahmen und feine Trennlinien.</summary>
    public const string Rahmen = "FFBFBFBF";

    /// <summary>Ueberschrift der Kennzahlenbloecke oben.</summary>
    public const string BlockHintergrund = "FFF2F2F2";

    public const string Schriftart = "Arial";
    public const double SchriftgroesseDaten = 9;
    public const double SchriftgroesseKopf = 9;
    public const double SchriftgroesseTitel = 12;

    /// <summary>Waehrungsformat fuer Kostenspalten.</summary>
    public const string WaehrungsFormat = "\"CHF\" #,##0.00";
}
