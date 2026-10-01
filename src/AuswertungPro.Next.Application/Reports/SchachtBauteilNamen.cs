namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Die kanonischen Bauteilnamen eines Schachts, wie sie der PDF-Schachtprotokollimport
/// (<c>SchachtProtocolParser</c>, Infrastructure) aus der Tabelle "Zustand der Schachtbauteile"
/// liest und unveraendert als <c>ProtocolEntry.Code</c> ablegt: "Schacht", "Schachtdeckel",
/// "Deckelrahmen", "Schachthals", "Konus", "Schachtrohr", "Bankett", "Durchlaufrinne",
/// "Anschluss", "Leiter/Steigeisen", "Tauchbogen".
///
/// EINE Wahrheit fuer beide Seiten: <c>SchachtProtocolParser.SchachtComponentOrder</c>
/// (Infrastructure) uebernimmt genau diese Liste fuer die Sortierreihenfolge; die Regeln in
/// dieser Datei (<c>SchachtSchadenKategorieRegel</c>, <c>SchachtSchadenOrtRegel</c>) pruefen
/// hier, ob ein <c>Code</c> ueberhaupt ein Bauteilname aus DIESEM Importweg ist — nicht z.B.
/// ein VSA-Code oder ein Freitext aus einer anderen Quelle.
/// </summary>
public static class SchachtBauteilNamen
{
    public static readonly IReadOnlyList<string> Kanonisch =
    [
        "Schacht", "Schachtdeckel", "Deckelrahmen", "Schachthals", "Konus", "Schachtrohr",
        "Bankett", "Durchlaufrinne", "Anschluss", "Leiter/Steigeisen", "Tauchbogen"
    ];

    /// <summary>True, wenn <paramref name="code"/> genau einem der kanonischen Bauteilnamen entspricht.</summary>
    public static bool IstBekannt(string? code)
    {
        var wert = (code ?? string.Empty).Trim();
        if (wert.Length == 0)
            return false;

        return Kanonisch.Any(name => string.Equals(name, wert, StringComparison.OrdinalIgnoreCase));
    }
}
