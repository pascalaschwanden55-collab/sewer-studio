using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>Fertige Schachtgrafik: SVG-Text, seine Masse, die Hinweisflaechen und die Legende.</summary>
public sealed record SchachtgrafikAnsicht(
    string Svg,
    int Breite,
    int Hoehe,
    IReadOnlyList<SchachtgrafikMarke> Marken,
    IReadOnlyList<SchachtgrafikLegende> Legende);

/// <summary>
/// Loest einen <see cref="SchachtRecord"/> samt Haltungen der Seite (und optional Lage und
/// Koten) zur fertigen <see cref="SchachtgrafikAnsicht"/> auf: erst das Modell
/// (<see cref="SchachtgrafikModellBuilder"/>), dann die Zeichnung
/// (<see cref="SchachtgrafikSvgBuilder"/>). WPF-frei, wie <see cref="HaltungsgrafikAnsichtBuilder"/>
/// fuer die Haltung — die Oberflaeche (<c>SchachtgrafikControl</c>) zeichnet danach nur noch.
///
/// Schachtfelder werden ausschliesslich ueber <see cref="SchachtFeldnamen"/> gelesen: Der
/// Datensatz fuehrt sie unter der Kopfzeile der Excel-Vorlage, nicht unter dem Katalognamen.
/// Haltungen, Lage und Koten kommen von der Seite (kein Service-Locator im Control).
///
/// Die Anzeige ist rein lesend: Sie veraendert weder Protokoll noch Datensatz.
/// </summary>
public static class SchachtgrafikAnsichtBuilder
{
    /// <summary>
    /// Markenfarbe der Grafik. Bewusst derselbe Standardwert wie bei der Haltungsgrafik: Die
    /// Oberflaeche bildet genau diesen Wert auf ihren Akzent-Token ab, damit in der Grafik keine
    /// feste Farbe steht.
    /// </summary>
    public const string Markenfarbe = "#006E9C";

    /// <summary>Baut die Grafik ohne Lage und Koten (bisheriger Aufruf).</summary>
    public static SchachtgrafikAnsicht? Baue(
        SchachtRecord? record,
        IReadOnlyList<HaltungRecord>? haltungen,
        ICodeCatalogProvider? catalog)
        => Baue(record, haltungen, catalog, zusatz: null);

    /// <summary>
    /// Baut die Grafik eines Schachts. Gibt <c>null</c> zurueck, wenn kein Schacht gewaehlt ist.
    /// <paramref name="zusatz"/> traegt Lage (QGIS-Kopie) und Koten (Objektakten); ohne beides
    /// bleiben Grundriss und Tiefen dort schematisch, wo der Datensatz nichts hergibt.
    /// </summary>
    public static SchachtgrafikAnsicht? Baue(
        SchachtRecord? record,
        IReadOnlyList<HaltungRecord>? haltungen,
        ICodeCatalogProvider? catalog,
        SchachtgrafikZusatz? zusatz)
    {
        if (record is null)
            return null;

        var modell = SchachtgrafikModellBuilder.Baue(record, haltungen, catalog, zusatz, Markenfarbe);
        var zeichnung = SchachtgrafikSvgBuilder.Baue(modell, Markenfarbe);
        return new SchachtgrafikAnsicht(zeichnung.Svg, zeichnung.Breite, zeichnung.Hoehe, zeichnung.Marken, zeichnung.Legende);
    }
}
