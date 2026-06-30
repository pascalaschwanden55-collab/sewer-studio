using System;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Bestimmt das Ziel fuer manuelle Verteilungen: bevorzugt den aktiven Projektordner,
/// Dialog nur als Rueckfall fuer nicht gespeicherte/alte Sonderfaelle.
/// </summary>
public static class DistributionTargetFolderPolicy
{
    public static string? Resolve(string? projectFolder, Func<string?> promptFallback)
    {
        ArgumentNullException.ThrowIfNull(promptFallback);

        return string.IsNullOrWhiteSpace(projectFolder)
            ? promptFallback()
            : projectFolder;
    }
}
