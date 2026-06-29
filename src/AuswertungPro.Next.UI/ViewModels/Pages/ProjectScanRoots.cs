using System.Collections.Generic;
using System.IO;

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
}
