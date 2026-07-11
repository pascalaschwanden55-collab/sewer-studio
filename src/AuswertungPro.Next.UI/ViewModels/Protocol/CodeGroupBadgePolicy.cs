namespace AuswertungPro.Next.UI.ViewModels.Protocol;

/// <summary>Badge-Zutaten fuer eine VSA-Hauptgruppe (Theme-BrushKeys + MDL2-Glyph).</summary>
public sealed record CodeGroupBadge(string BrushKey, string SubtleBrushKey, string Glyph, string Kurzlabel);

/// <summary>
/// VSA-Hauptgruppe -> Badge-Farben/Symbol. Eigene Hues (KEINE Ampelfarben),
/// damit Schadensgruppen und Bewertung nie verwechselt werden.
/// BA = strukturelle Schaeden, BB = betriebliche Stoerungen, BC = Bestandsaufnahme.
/// </summary>
public static class CodeGroupBadgePolicy
{
    // Glyphs: Segoe MDL2 Assets — Warnung (BA), Werkzeug (BB), Verbindung (BC), Info (Rest).
    private static readonly CodeGroupBadge Struktur =
        new("CodeGroupStrukturBrush", "CodeGroupStrukturSubtleBrush", "", "Struktur");

    private static readonly CodeGroupBadge Betrieb =
        new("CodeGroupBetriebBrush", "CodeGroupBetriebSubtleBrush", "", "Betrieb");

    private static readonly CodeGroupBadge Bestand =
        new("CodeGroupBestandBrush", "CodeGroupBestandSubtleBrush", "", "Bestand");

    private static readonly CodeGroupBadge Sonstig =
        new("CodeGroupSonstigBrush", "CodeGroupSonstigSubtleBrush", "", "Sonstig");

    public static CodeGroupBadge Resolve(string? code)
    {
        var text = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(text) || text.Length < 2)
            return Sonstig;

        return text[..2] switch
        {
            "BA" => Struktur,
            "BB" => Betrieb,
            "BC" => Bestand,
            _ => Sonstig
        };
    }
}
