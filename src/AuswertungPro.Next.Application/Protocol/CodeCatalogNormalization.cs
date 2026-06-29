namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Gemeinsamer statischer Helfer fuer die Normalisierung von Code-Eintraegen.
/// JsonCodeCatalogProvider und ManifestCodeCatalogProvider delegieren hierher;
/// kein Duplikat mehr in den einzelnen Klassen.
/// XmlCodeCatalogProvider und DeduplicateCodes bleiben bewusst aussen vor
/// (abweichende Semantik: kein ToUpperInvariant bzw. unterschiedliche Merge-Strategie).
/// </summary>
internal static class CodeCatalogNormalization
{
    /// <summary>
    /// Normalisiert einen einzelnen Code-String: Trim + UpperInvariant.
    /// </summary>
    internal static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Erstellt normalisierte Kopien aller Eintraege (Clone + NormalizeCodeDefinition).
    /// </summary>
    internal static List<CodeDefinition> NormalizeCodes(IEnumerable<CodeDefinition> codes)
        => (codes ?? Array.Empty<CodeDefinition>())
            .Select(CodeDefinitionCloning.CloneCode)
            .Select(NormalizeCodeDefinition)
            .ToList();

    /// <summary>
    /// Normalisiert alle Felder einer bereits geklonten <see cref="CodeDefinition"/> in-place und gibt sie zurueck.
    /// </summary>
    internal static CodeDefinition NormalizeCodeDefinition(CodeDefinition code)
    {
        code.Code = NormalizeCode(code.Code);
        code.Title = (code.Title ?? string.Empty).Trim();
        code.CanonicalCode = string.IsNullOrWhiteSpace(code.CanonicalCode)
            ? code.Code
            : NormalizeCode(code.CanonicalCode);
        code.Source = string.IsNullOrWhiteSpace(code.Source) ? null : code.Source.Trim();
        code.StandardAnnotation = string.IsNullOrWhiteSpace(code.StandardAnnotation) ? null : code.StandardAnnotation.Trim();
        code.Group = string.IsNullOrWhiteSpace(code.Group) ? "Unbekannt" : code.Group.Trim();
        code.Description = string.IsNullOrWhiteSpace(code.Description) ? null : code.Description.Trim();
        code.CategoryPath = (code.CategoryPath ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        code.Parameters = (code.Parameters ?? new List<CodeParameter>())
            .Select(CodeDefinitionCloning.CloneParameter)
            .ToList();
        code.Examples = (code.Examples ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        if (code.RangeThresholdM is not null && code.RangeThresholdM <= 0)
            code.RangeThresholdM = null;

        return code;
    }

    /// <summary>
    /// Liefert alle selektierbaren, nicht beobachtungs-erweiterten Codes alphabetisch sortiert.
    /// </summary>
    internal static IReadOnlyList<string> AllowedCodes(IEnumerable<CodeDefinition> codes)
        => codes
            .Where(x => x.IsSelectable && !x.IsObservedExtension)
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
