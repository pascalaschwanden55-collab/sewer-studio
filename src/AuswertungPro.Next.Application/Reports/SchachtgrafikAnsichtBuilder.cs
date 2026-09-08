using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>Fertige Schachtgrafik: SVG-Text, seine Masse und die Hinweisflaechen.</summary>
public sealed record SchachtgrafikAnsicht(string Svg, int Breite, int Hoehe, IReadOnlyList<SchachtgrafikMarke> Marken);

/// <summary>
/// Loest einen <see cref="SchachtRecord"/> samt angeschlossener Haltungen zu einer fertigen
/// <see cref="SchachtgrafikAnsicht"/> auf. WPF-frei, wie <see cref="HaltungsgrafikAnsichtBuilder"/>
/// fuer die Haltung — die Oberflaeche (<c>SchachtgrafikControl</c>) zeichnet danach nur noch.
///
/// Schachtfelder werden ausschliesslich ueber <see cref="SchachtFeldnamen"/> gelesen: Der
/// Datensatz fuehrt sie unter der Kopfzeile der Excel-Vorlage, nicht unter dem Katalognamen.
/// Angeschlossene Haltungen kommen von der Seite (kein Service-Locator im Control).
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

    /// <summary>
    /// Baut die Grafik eines Schachts. Gibt <c>null</c> zurueck, wenn kein Schacht gewaehlt ist.
    /// Anders als bei der Haltung braucht die Schachtgrafik keine Laenge — der senkrechte
    /// Schnitt zeigt feste Zonen unabhaengig von der Tiefe; fehlt die Tiefe, bleibt nur die
    /// Masslinie weg (siehe <see cref="SchachtgrafikSvgBuilder"/>).
    /// </summary>
    public static SchachtgrafikAnsicht? Baue(
        SchachtRecord? record,
        IReadOnlyList<HaltungRecord>? haltungen,
        ICodeCatalogProvider? catalog)
    {
        if (record is null)
            return null;

        var schachtnummer = Wert(record, "Schachtnummer");
        var tiefe = ParseTiefeMeter(Wert(record, "Schachttiefe"));
        var dimension1 = Wert(record, FieldKeys.ShaftDimension1Mm);
        var dimension2 = Wert(record, FieldKeys.ShaftDimension2Mm);

        var zulaeufe = Stummel(haltungen, schachtnummer, "Schacht_unten");
        var ablaeufe = Stummel(haltungen, schachtnummer, "Schacht_oben");

        var eintraege = (record.Protocol?.Current?.Entries ?? [])
            .Where(e => !e.IsDeleted)
            .ToList();
        var schaeden = eintraege
            .Select(e =>
            {
                // Der PDF-Schachtprotokollimport traegt im Code den Bauteilnamen, keinen VSA-Code —
                // SchachtSchadenKategorieRegel ergaenzt deshalb den bekannten Schadenstext als
                // Fallback, statt jeden Schaden generisch darzustellen (siehe dortige Grenzen).
                var kategorie = SchachtSchadenKategorieRegel.Bestimme(e.Code, e.Beschreibung);
                return new SchachtgrafikSchadenEintrag(
                    SchachtSchadenOrtRegel.Bestimme(e),
                    kategorie,
                    DamageSymbolClassifier.GetDamageSymbolColor(kategorie, Markenfarbe),
                    Hinweistext(e, catalog));
            })
            .ToList();

        var (svg, marken) = SchachtgrafikSvgBuilder.Baue(
            schachtnummer, tiefe, dimension1, dimension2, zulaeufe, ablaeufe, schaeden, Markenfarbe);

        return new SchachtgrafikAnsicht(svg, SchachtgrafikSvgBuilder.Width, SchachtgrafikSvgBuilder.Height, marken);
    }

    /// <summary>Hinweistext einer Hinweisflaeche: Code, und bei bekanntem Katalog der Klartext.</summary>
    private static string Hinweistext(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
        var klartext = ObservationZustandBuilder.Build(entry, catalog);
        return string.IsNullOrWhiteSpace(klartext) || klartext == "-" ? code : $"{code} — {klartext}";
    }

    /// <summary>
    /// Angeschlossene Haltungen einer Seite: <paramref name="feld"/> ist <c>Schacht_unten</c>
    /// fuer den Zulauf und <c>Schacht_oben</c> fuer den Ablauf dieses Schachts.
    /// </summary>
    private static IReadOnlyList<SchachtgrafikStummel> Stummel(
        IReadOnlyList<HaltungRecord>? haltungen, string? schachtnummer, string feld)
    {
        if (haltungen is null || string.IsNullOrWhiteSpace(schachtnummer))
            return [];

        var gesucht = schachtnummer.Trim();
        return haltungen
            .Where(h => string.Equals((h.GetFieldValue(feld) ?? "").Trim(), gesucht, StringComparison.OrdinalIgnoreCase))
            .Select(h => new SchachtgrafikStummel(
                (h.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim(),
                DnText(h.GetFieldValue(FieldKeys.NominalDiameterMm))))
            .ToList();
    }

    private static string? DnText(string? dn)
        => string.IsNullOrWhiteSpace(dn) ? null : $"DN{dn.Trim()}";

    /// <summary>
    /// Liest die Schachttiefe als Meterwert. Der Vorlagen-/PDF-Import normiert
    /// <c>Schachttiefe</c> bereits auf eine reine Meterzahl ohne Einheit; ein von Hand mit
    /// Einheit erfasster Wert ("2.40 m") wird zusaetzlich toleriert. Andere Einheiten (cm, mm)
    /// werden NICHT geraten — ohne belegte Schreibweise waere eine Umrechnung eine erfundene
    /// Annahme.
    /// </summary>
    private static double? ParseTiefeMeter(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;

        if (wert.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            wert = wert[..^1].TrimEnd();

        return FachzahlParser.TryParseMeasurement(wert, out var zahl) && zahl > 0
            ? (double)zahl
            : null;
    }

    /// <summary>Feldwert eines Schachts unter der Schreibweise, die der Datensatz wirklich fuehrt.</summary>
    private static string? Wert(SchachtRecord record, string feld)
        => record.GetFieldValue(SchachtFeldnamen.Feld(record, feld));
}
