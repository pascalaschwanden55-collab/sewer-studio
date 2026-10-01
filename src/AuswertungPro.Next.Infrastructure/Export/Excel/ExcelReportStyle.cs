using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

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

    /// <summary>
    /// Eigentuemer, damit die Zustaendigkeit auf einen Blick sichtbar ist. Zuerst die Begriffe des Programms,
    /// danach die WebGIS-Namen (siehe <see cref="MitWebGisNamen"/>).
    /// </summary>
    public static IReadOnlyList<ExcelFarbregel> Eigentuemer { get; } = MitWebGisNamen(new[]
    {
        // Amtlicher Begriff des Kantons und die Kurzform aus Altprojekten
        // tragen dieselbe Farbe. Ein nachgeschlagener Wert soll gefaerbt sein,
        // ein gewachsener Bestand seine Farbe behalten; die Vorlage vergleicht
        // exakt, deshalb braucht jede Schreibweise ihre eigene Regel.
        new ExcelFarbregel("Abwasser Uri", "FF548235"),
        new ExcelFarbregel("AWU", "FF548235"),
        new ExcelFarbregel("Kanton Uri", "FFFFFF00"),
        new ExcelFarbregel("Kanton", "FFFFFF00"),
        new ExcelFarbregel("Bund", "FFFF8000"),
        new ExcelFarbregel("Gemeinde", "FF00B0F0"),
        new ExcelFarbregel("Privat", "FFFF0000")
    });

    /// <summary>
    /// Seit das Holen den Eigentuemer wie im WebGIS setzt (Entscheid Pascal 24.09.2026), stehen dort WebGIS-Namen:
    /// «AWU_von_privat», «Altdorf», an der Haltung mit Typ «Altdorf (Gemeinde)». Jeder zaehlt und faerbt nach seinem
    /// WebGIS-Typ (<see cref="WebGisOrganisationen"/>) wie die Kategorie der Vorlage. Genossenschaften und
    /// «Unbekannt» haben dort keine Kategorie und bleiben wie bisher ungefaerbt. Keine Schreibweise doppelt,
    /// auch nicht in anderer Gross-/Kleinschreibung — Excel vergleicht ohne, die Zeile zaehlte sonst zweimal.
    /// Der Vorlagenbauer (<c>tools/ExcelVorlagenBauer/vorlage.py</c>) liest dieselbe Liste aus dem Katalog.
    /// </summary>
    private static IReadOnlyList<ExcelFarbregel> MitWebGisNamen(ExcelFarbregel[] begriffe)
    {
        var farbeJeTyp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abwasserverband"] = "FF548235", // wie Abwasser Uri
            ["Kanton"] = "FFFFFF00",
            ["Bund"] = "FFFF8000",
            ["Gemeinde"] = "FF00B0F0",
            ["Privat"] = "FFFF0000",
        };
        var liste = new List<ExcelFarbregel>(begriffe);
        foreach (var organisation in WebGisOrganisationen.Alle)
        {
            if (!farbeJeTyp.TryGetValue(organisation.WebGisTyp, out var farbe)) continue;
            foreach (var wert in new[] { organisation.Name, $"{organisation.Name} ({organisation.WebGisTyp})" })
                if (liste.TrueForAll(r => !string.Equals(r.Wert, wert, StringComparison.OrdinalIgnoreCase)))
                    liste.Add(new ExcelFarbregel(wert, farbe));
        }
        return liste;
    }

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
