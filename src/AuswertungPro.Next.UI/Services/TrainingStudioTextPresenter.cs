namespace AuswertungPro.Next.UI.Services;

/// <summary>Formatiert Code- und Beschreibungstexte des Training Studios.</summary>
internal sealed class TrainingStudioTextPresenter
{
    private readonly Func<string, string?> _codeLabelLookup;

    public TrainingStudioTextPresenter(Func<string, string?> codeLabelLookup)
        => _codeLabelLookup = codeLabelLookup;

    public string BuildBeschreibungVorlage(string? code, double? clock)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        var normalizedCode = NormalizeCode(code);
        var label = _codeLabelLookup(normalizedCode) ?? normalizedCode;
        return clock.HasValue
            ? $"{label} bei {clock.Value:0.#} Uhr — Ausmass ergaenzen"
            : $"{label} — Lage und Ausmass ergaenzen";
    }

    public string BuildKatalogBeschreibung(
        string? code,
        string? katalogBeschreibung,
        double? clock,
        int? severity)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var normalizedCode = NormalizeCode(code);
        var label = string.IsNullOrWhiteSpace(katalogBeschreibung)
            ? _codeLabelLookup(normalizedCode)
            : katalogBeschreibung.Trim();
        var beschreibung = string.IsNullOrWhiteSpace(label)
            ? $"VSA-Code {normalizedCode}"
            : $"{normalizedCode} — {label}";

        if (clock.HasValue)
            beschreibung += $", bei {clock.Value:0.#} Uhr";
        if (severity.HasValue)
            beschreibung += $", Schadensstufe {severity.Value}";

        return beschreibung;
    }

    public string ResolveCodeLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        return _codeLabelLookup(NormalizeCode(code))
               ?? "Code nicht im VSA-Katalog gefunden";
    }

    public string NormalizeCode(string? code)
        => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
