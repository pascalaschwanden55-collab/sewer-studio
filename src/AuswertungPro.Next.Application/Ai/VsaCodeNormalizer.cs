namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Gemeinsame Normalisierungs-Helfer fuer VSA-Codes (Application-Schicht).
/// Frei von Domain-Abhaengigkeiten, rein statisch und testbar.
/// </summary>
internal static class VsaCodeNormalizer
{
    /// <summary>
    /// Liefert den dreistelligen Hauptcode eines VSA-Codes (z.B. "BAB" aus "BAB.A"),
    /// oder <see langword="null"/> wenn der Code leer oder kuerzer als 3 Zeichen ist.
    /// Normalisierung: Whitespace entfernen, Punkt entfernen, Grossbuchstaben.
    /// </summary>
    internal static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var t = code.Trim().Replace(".", "").ToUpperInvariant();
        return t.Length >= 3 ? t[..3] : null;
    }
}
