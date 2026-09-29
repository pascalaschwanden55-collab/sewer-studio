using System;
using System.IO;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Regel, welcher Logo-Pfad fuer Berichte gilt: eine gesetzte Einstellung geht
/// vor, sonst das mitgelieferte Standardlogo neben dem Programm. Existiert keine
/// gueltige Datei, gibt es kein Logo (<c>null</c>) statt eines erfundenen Pfads.
///
/// Kein echter Dateizugriff in dieser Klasse selbst — <c>fileExists</c> wird
/// hereingereicht, damit die Regel ohne Datentraeger testbar bleibt. Verwendet von
/// <c>AppSettingsBerichtsMarke</c> (produktiv) und von einzelnen Exportern, die den
/// reinen Standardpfad brauchen, ohne selbst eine eigene Zeichenkette zu bauen.
/// </summary>
public static class BerichtsLogoResolver
{
    /// <summary>Relativer Standardpfad neben dem Programmordner.</summary>
    private static readonly string[] DefaultRelativeSegments = ["Assets", "Brand", "abwasser-uri-logo.png"];

    public static string? Resolve(string? configuredPath, string appBaseDirectory, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!string.IsNullOrWhiteSpace(configuredPath) && fileExists(configuredPath))
            return configuredPath;

        var defaultPath = DefaultLogoPath(appBaseDirectory);
        return fileExists(defaultPath) ? defaultPath : null;
    }

    /// <summary>Der Standardpfad selbst, ohne Pruefung ob die Datei existiert.</summary>
    public static string DefaultLogoPath(string appBaseDirectory)
        => Path.Combine([appBaseDirectory ?? string.Empty, .. DefaultRelativeSegments]);
}
