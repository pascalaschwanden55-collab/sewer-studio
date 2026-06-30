namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Berechnet das Badge-Kuerzel fuer eine Katalog-Quelle (z.B. "ICM", "WinCan").
/// Reine Funktion ohne Seiteneffekte; aus CodeTreeNode.BuildSourceBadge extrahiert.
/// </summary>
public static class CodeSourceBadgeFormatter
{
    /// <summary>
    /// Gibt das Badge-Label fuer eine Source-Bezeichnung zurueck.
    /// ILI-Quellen erhalten kein Badge (Leerstring); alle anderen werden kurz beschriftet.
    /// </summary>
    public static string GetBadgeText(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        return source.Trim() switch
        {
            VsaKekCatalogSources.Ili         => string.Empty,
            VsaKekCatalogSources.Icm         => "ICM",
            VsaKekCatalogSources.XtfObserved => "XTF",
            VsaKekCatalogSources.WinCanFallback => "WinCan",
            var other                         => other
        };
    }
}
