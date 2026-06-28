namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Allgemeine reine Schluessel-Helfer, die nicht bereits in HoldingIdNormalizer abgedeckt sind.
/// Extrahiert aus HoldingFolderDistributor.VideoMatching – verhaltensneutral.
/// </summary>
internal static class HoldingKeyUtils
{
    /// <summary>
    /// Gibt den Suffix ab dem ersten Unterstrich zurueck (inklusive Unterstrich).
    /// Gibt null zurueck wenn kein Unterstrich vorhanden.
    /// </summary>
    internal static string? GetSuffixFromFirstUnderscore(string fileName)
    {
        var idx = fileName.IndexOf('_');
        if (idx < 0)
            return null;
        return fileName.Substring(idx);
    }
}
