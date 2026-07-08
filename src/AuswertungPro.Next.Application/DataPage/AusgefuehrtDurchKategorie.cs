using System;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Ordnet den Feldwert "Ausgefuehrt_durch" (Baumeister / Kanalsanierer / Gartenbauer / leer)
/// einer kanonischen Kategorie zu. Genutzt fuer den QGIS-Layer "Ausgefuehrt durch", der die
/// Haltungen nach dem Ausfuehrenden einfaerbt. Rein, null-sicher, unit-testbar.
/// Wort-/Prioritaets-Logik konsistent zu
/// <c>ZustandsklasseCellStyleFactory.MapAusgefuehrtDurchBackground</c> (Baumeister gewinnt).
/// </summary>
public static class AusgefuehrtDurchKategorie
{
    public const string Baumeister = "Baumeister";
    public const string Sanierer = "Sanierer";
    public const string Gartenbauer = "Gartenbauer";

    /// <summary>
    /// Liefert die kanonische Kategorie ("Baumeister" / "Sanierer" / "Gartenbauer") oder ""
    /// wenn kein Ausfuehrender gesetzt/erkennbar ist. "Kanalsanierer" faellt auf "Sanierer".
    /// </summary>
    public static string Resolve(string? ausgefuehrtDurch)
    {
        if (string.IsNullOrWhiteSpace(ausgefuehrtDurch))
            return "";

        var key = ausgefuehrtDurch.Trim().ToLowerInvariant();

        // Baumeister gewinnt (analog Zellfarb-Logik).
        if (key.Contains("baumeister", StringComparison.Ordinal))
            return Baumeister;
        if (key.Contains("sanierer", StringComparison.Ordinal)) // deckt auch "kanalsanierer" ab
            return Sanierer;
        if (key.Contains("gartenbauer", StringComparison.Ordinal)
            || key.Contains("gärtner", StringComparison.Ordinal)
            || key.Contains("gaertner", StringComparison.Ordinal))
            return Gartenbauer;

        return "";
    }
}
