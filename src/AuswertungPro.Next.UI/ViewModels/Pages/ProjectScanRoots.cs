using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Liefert die Verzeichnisse, in denen die Projektliste nach *.json sucht.
/// Pur (kein Dateisystem-Zugriff) — die tatsaechliche Existenzpruefung/Enumeration
/// macht der Aufrufer.
/// </summary>
public static class ProjectScanRoots
{
    public static IReadOnlyList<string> Resolve(string currentDirectory, string? projectsRootDirectory)
    {
        var roots = new List<string>
        {
            Path.Combine(currentDirectory, "Rohdaten"),
            Path.Combine(currentDirectory, "Rohdaten", "Section_PDF")
        };

        // Konfiguriertes Projekte-Verzeichnis (falls gesetzt) bevorzugt aufnehmen.
        if (!string.IsNullOrWhiteSpace(projectsRootDirectory))
            roots.Add(projectsRootDirectory);

        return roots;
    }

    /// <summary>
    /// Vollstaendige Basisordner-Liste fuer den Projektlisten-Scan: Resolve()
    /// plus die aus letztem Projekt/Merkliste GELERNTEN Wurzeln plus
    /// Standard-Fallbacks. So bleibt die Liste auch dann gefuellt, wenn die
    /// Settings-Merkliste verloren geht.
    /// </summary>
    public static IReadOnlyList<string> ResolveAll(
        string currentDirectory,
        string? projectsRootDirectory,
        string? lastProjectPath,
        IEnumerable<string>? recentProjectPaths)
    {
        var roots = new List<string>(Resolve(currentDirectory, projectsRootDirectory));

        // Wurzel aus bekannten Projektdateien lernen: Eltern-Ordner des Projekt-Roots
        // (z.B. D:\Projekte\Zone 1.15\x.json -> D:\Projekte). So findet der Scan auch
        // dann alles, wenn kein Projekte-Verzeichnis konfiguriert ist.
        AddParentOfProjectFile(roots, lastProjectPath);
        if (recentProjectPaths is not null)
        {
            foreach (var recent in recentProjectPaths)
                AddParentOfProjectFile(roots, recent);
        }

        // Standard-Speicherorte (mit und ohne "e" — beide Schreibweisen kommen vor).
        roots.Add(@"D:\Projekt");
        roots.Add(@"D:\Projekte");
        roots.Add(@"C:\Projekt");
        roots.Add(@"C:\Projekte");

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            if (seen.Add(root))
                result.Add(root);
        }

        return result;
    }

    private static void AddParentOfProjectFile(List<string> roots, string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return;

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        var parent = Path.GetDirectoryName(projectRoot);
        if (!string.IsNullOrWhiteSpace(parent))
            roots.Add(parent);
    }
}
