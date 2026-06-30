namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Schreibt Katalog-Metadaten (Source, CanonicalCode, StandardAnnotation) in ein Parameter-Dictionary.
/// Reine Funktion ohne Seiteneffekte; aus ProtocolCodePickerViewModel extrahiert.
/// </summary>
public static class CatalogMetadataWriter
{
    /// <summary>
    /// Fuegt catalog.source, catalog.canonicalCode und catalog.standardAnnotation
    /// aus der <paramref name="code"/>-Definition in <paramref name="parameters"/> ein,
    /// sofern die jeweiligen Werte nicht leer sind.
    /// </summary>
    public static void AddCatalogMetadata(
        Dictionary<string, string> parameters,
        CodeDefinition code)
    {
        AddIfPresent(parameters, "catalog.source", code.Source);
        AddIfPresent(parameters, "catalog.canonicalCode", code.CanonicalCode);
        AddIfPresent(parameters, "catalog.standardAnnotation", code.StandardAnnotation);
    }

    /// <summary>
    /// Fuegt einen Eintrag in das Dictionary ein, wenn <paramref name="value"/> nicht leer ist.
    /// </summary>
    private static void AddIfPresent(Dictionary<string, string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parameters[key] = value.Trim();
    }
}
