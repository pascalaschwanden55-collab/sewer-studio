using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Reine Auswahlregel der Programm-Momentaufnahme (kein IO).
///
/// Die Momentaufnahme ist bewusst NICHT der rohe Ordner. Vom Programmordner sind
/// ueber 99 Prozent der Dateien ableitbar: Kartenkacheln, Build-Ausgabe, die
/// Python-Umgebung und Arbeitsreste. Sie machen die Sicherung gross und langsam,
/// tragen aber keine einzige Information, die verloren gehen koennte.
///
/// Uebrig bleibt genau das, woraus sich der Rest wieder erzeugen laesst:
/// Quellcode, der vollstaendige Git-Verlauf und die Modellgewichte.
/// Ausgeschlossen wird nur ausdruecklich Benanntes (sichere Richtung:
/// Unbekanntes wandert mit).
/// </summary>
public static class ProgramSnapshotFileCatalog
{
    /// <summary>
    /// Zusaetzlich zu <see cref="BackupExclusionRules.IsProgramDirExcluded"/>
    /// ausgeschlossene Ordner. Beide Namen sind reine Arbeitsstaende:
    /// <c>basemap_tiles</c> sind nachladbare Kartenkacheln (allein rund 600'000
    /// Dateien), <c>.worktrees</c> sind Git-Arbeitskopien, deren Inhalt bereits
    /// im mitgesicherten <c>.git</c> steckt.
    /// </summary>
    private static readonly string[] AdditionalExcludedNames =
        { "basemap_tiles", ".worktrees", ".playwright-cli" };

    /// <summary>
    /// Meldet, ob ein Ordner der Momentaufnahme fernbleibt. Der Pfad ist relativ
    /// zur Programmwurzel; geprueft wird jede Ebene, damit ein tief liegendes
    /// <c>bin</c> genauso greift wie eines direkt an der Wurzel.
    /// </summary>
    public static bool IsExcludedDirectory(string relativeDirPath)
    {
        if (string.IsNullOrWhiteSpace(relativeDirPath))
            return false;

        return BackupExclusionRules.IsProgramDirExcluded(relativeDirPath)
               || NameMatches(LastSegment(relativeDirPath), AdditionalExcludedNames);
    }

    /// <summary>
    /// Meldet, ob irgendeine Ebene des relativen Pfads ausgeschlossen ist. Damit
    /// entscheidet eine einzelne Datei genauso wie der Ordnerdurchlauf, ohne dass
    /// beide Wege dieselbe Regel doppelt auslegen.
    /// </summary>
    public static bool IsExcludedPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        // Der letzte Abschnitt ist der Dateiname; er wird nicht als Ordner geprueft.
        return segments
            .Take(Math.Max(0, segments.Length - 1))
            .Any(IsExcludedDirectory);
    }

    private static string LastSegment(string relativeDirPath)
    {
        var trimmed = relativeDirPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private static bool NameMatches(string name, string[] candidates)
        => candidates.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
}
