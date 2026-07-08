using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Regeln fuer den Versions-Ordner der Datensicherung (reine Logik, kein IO).
/// Ersetzte und im Spiegel entfallene Dateien wandern pro Lauf in einen
/// datierten Stand unter "_Versionen" statt endgueltig geloescht zu werden.
/// So uebertraegt ein Sicherungslauf versehentliche Loeschungen oder kaputte
/// Dateien nicht mehr unumkehrbar in die Sicherung.
/// </summary>
public static class BackupVersionRetention
{
    /// <summary>Ordnername im Spiegel-Root, unter dem aeltere Dateistaende liegen.</summary>
    public const string VersionsFolderName = "_Versionen";

    /// <summary>Wie viele Staende aufbewahrt werden — aelteste werden beim Lauf entfernt.</summary>
    public const int MaxStaende = 10;

    private const string StandNameFormat = "yyyy-MM-dd_HHmmss";

    /// <summary>Name eines Stand-Ordners aus der lokalen Startzeit des Laufs (sortierbar).</summary>
    public static string BuildStandName(DateTime lokaleStartzeit)
        => lokaleStartzeit.ToString(StandNameFormat, CultureInfo.InvariantCulture);

    /// <summary>Erkennt Ordnernamen, die von <see cref="BuildStandName"/> erzeugt wurden.</summary>
    public static bool IsStandName(string name)
        => DateTime.TryParseExact(
            name,
            StandNameFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    /// <summary>Relativer Zielpfad einer Datei innerhalb des Versions-Ordners.</summary>
    public static string BuildVersionsRelativePath(string standName, string targetRelativePath)
        => Path.Combine(VersionsFolderName, standName, targetRelativePath);

    /// <summary>Liegt der relative Ordnerpfad (zum Spiegel-Root) im Versions-Ordner?</summary>
    public static bool IsVersionsDir(string relativeDirPath)
    {
        var first = relativeDirPath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)[0];
        return string.Equals(first, VersionsFolderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Waehlt die zu entfernenden aeltesten Staende aus. Ordner, deren Name nicht
    /// dem Stand-Muster entspricht, werden nie zurueckgegeben (sichere Richtung).
    /// </summary>
    public static IReadOnlyList<string> SelectStaendeToDelete(
        IEnumerable<string> standNames,
        int maxKeep = MaxStaende)
        => standNames
            .Where(IsStandName)
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .Skip(maxKeep)
            .ToArray();
}
