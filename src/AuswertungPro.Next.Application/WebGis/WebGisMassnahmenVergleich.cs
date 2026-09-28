using System;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Die EINE Regel «ist das dieselbe Sanierungsmassnahme?» fuer Senden (Plan und Nachpruefung vor dem Anlegen)
/// und Holen. Gleich heisst: Art, Status und Verfahren stimmen ueberein (gefaltet wie beim Holen: Gross/Klein,
/// Umlaute, Unterstrich) UND das Jahr ist nicht nachweislich verschieden.
///
/// Pruefung 22.09.2026: Ohne Jahr galten eine Reparatur 2020 und eine Reparatur 2026 als eine Massnahme; die
/// zweite ging nie ins WebGIS und kam nie zurueck. Jetzt machen zwei BEKANNTE, verschiedene Jahre zwei Massnahmen.
/// Fehlt ein Jahr auf einer Seite, gilt die Massnahme als vorhanden: Eine doppelte Massnahme im Kataster ist
/// schlimmer als eine fehlende, die der Bericht als «bereits vorhanden» nennt. Live gelesen 28.09.2026: Die erste
/// Listenspalte ist «Zeitpunkt», nicht das Sanierungsjahr, und leer — das Jahr kommt deshalb aus der Massnahme
/// selbst (<see cref="WebGisMassnahmenJahr"/>).
/// </summary>
public static class WebGisMassnahmenVergleich
{
    /// <summary>Dieselbe Massnahme wie die Zeile der WebGIS-Liste? Jahr als «2026» oder als Datum.</summary>
    public static bool Gleich(WebGisSanierungZeile vorhanden, string? art, string? status, string? verfahren, string? jahr)
        => GleicherInhalt(vorhanden, art, status, verfahren) && !NachweislichAndereJahre(vorhanden.Jahr, jahr);

    /// <summary>Art, Status und Verfahren gleich — ohne Blick aufs Jahr.</summary>
    public static bool GleicherInhalt(WebGisSanierungZeile vorhanden, string? art, string? status, string? verfahren)
    {
        ArgumentNullException.ThrowIfNull(vorhanden);
        return GleicherText(vorhanden.Art, art) && GleicherText(vorhanden.Status, status) && GleicherText(vorhanden.Verfahren, verfahren);
    }

    /// <summary>Beide Jahre lesbar und verschieden. Ein fehlendes oder unlesbares Jahr beweist nichts.</summary>
    public static bool NachweislichAndereJahre(string? jahrOderDatumA, string? jahrOderDatumB)
    {
        var a = WebGisSanierungFeldkarte.JahrAusDatum(jahrOderDatumA);
        var b = WebGisSanierungFeldkarte.JahrAusDatum(jahrOderDatumB);
        return a is not null && b is not null && !string.Equals(a, b, StringComparison.Ordinal);
    }

    private static bool GleicherText(string? a, string? b) => WebGisHandwertKarte.Falte(a) == WebGisHandwertKarte.Falte(b);
}
