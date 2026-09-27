namespace AuswertungPro.Next.Application.UseCases.CodingClassifierHint;

/// <summary>
/// Bewahrt ein bereits aufgeloestes Bildsignal fuer die manuelle Pruefung.
/// Liefert weder einen Ereignisentwurf noch eine Zuordnung zu einer Maske.
/// </summary>
public sealed record CodingClassifierImageHint(string Code, string Label, double Confidence)
{
    public string Status => $"Bildhinweis: {Label} – bitte prüfen";
    public string Detail => $"Ungeprüfte Bildklassifikation (Modellwert {Confidence:P0}). "
                            + "Position, Uhrlage, Ausmass und Untercode sind damit nicht bestimmt.";

    public static CodingClassifierImageHint? Create(string? code, double? confidence, string? catalogLabel)
    {
        // Schadensklassen des vorhandenen Bildklassifikators. Struktur-/Grenzcodes
        // besitzen eigene Wege; LEER und unbekannte Codes sind kein Schadenshinweis.
        if (code is not ("BAB" or "BAF" or "BAI" or "BAJ" or "BBA" or "BBB")
            || confidence is not { } value || !double.IsFinite(value) || value <= 0 || value > 1)
            return null;

        return new(code,
            string.IsNullOrWhiteSpace(catalogLabel) ? $"Schadensgruppe {code}" : catalogLabel,
            value);
    }

    // Der Statusbalken kuerzt lange Texte; der neue Hinweis muss vorne stehen.
    public string BuildStatus(string status) => $"{Status} | {status}";

    public string AppendToDetail(string? detail)
        => string.IsNullOrWhiteSpace(detail) ? Detail : $"{detail} | {Detail}";
}
