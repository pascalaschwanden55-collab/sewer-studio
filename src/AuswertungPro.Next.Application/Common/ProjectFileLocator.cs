using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Findet/plant die projekt.json eines Projektordners — rueckwaertskompatibel: neue Projekte legen die
/// Datei unter &lt;Projekt&gt;\Projektdateien\projekt.json ab, Alt-Projekte liegen direkt im Root. Liefert aus
/// einem projekt.json-Pfad den echten Projekt-Root (Eltern von "Projektdateien", sonst das Verzeichnis),
/// damit relative Medienpfade weiterhin korrekt gegen den Projekt-Root aufloesen.
/// </summary>
public static class ProjectFileLocator
{
    public const string ProjektdateienDir = "Projektdateien";
    public const string ProjectFileName = "projekt.json";
    public const string RootPointerFileName = "projekt.pointer";

    /// <summary>Findet die projekt.json eines Projektordners: zuerst Projektdateien\, dann Root. Null, wenn keine existiert.</summary>
    public static string? Locate(string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder))
            return null;

        var inSub = Path.Combine(projectFolder, ProjektdateienDir, ProjectFileName);
        if (File.Exists(inSub))
            return inSub;

        var inRoot = Path.Combine(projectFolder, ProjectFileName);
        return File.Exists(inRoot) ? inRoot : null;
    }

    /// <summary>Zielpfad fuer NEUE Projekte: &lt;Projekt&gt;\Projektdateien\projekt.json.</summary>
    public static string TargetPath(string projectFolder)
        => Path.Combine(projectFolder, ProjektdateienDir, ProjectFileName);

    /// <summary>
    /// Projekt-Root aus einem projekt.json-Pfad: liegt die Datei direkt in einem "Projektdateien"-Ordner,
    /// ist der Root dessen Eltern-Ordner; sonst das Verzeichnis der Datei (Alt-Projekte im Root).
    /// </summary>
    public static string? ProjectRootFromFile(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        var dir = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        if (string.Equals(Path.GetFileName(dir), ProjektdateienDir, StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(dir) ?? dir;

        return dir;
    }

    /// <summary>Schreibt einen Root-Pointer (&lt;Projekt&gt;\projekt.pointer) mit dem relativen Pfad zur projekt.json — best effort.</summary>
    public static void WriteRootPointer(string projectFolder, string projectFilePath)
    {
        try
        {
            var rel = Path.GetRelativePath(projectFolder, projectFilePath);
            AtomicTextFileWriter.WriteAllText(Path.Combine(projectFolder, RootPointerFileName), rel);
        }
        catch
        {
            // best effort — ein fehlender Pointer darf die Projekterstellung nicht stoeren
        }
    }
}
