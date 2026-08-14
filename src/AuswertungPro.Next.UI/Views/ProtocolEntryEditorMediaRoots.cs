using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views;

/// <summary>
/// Ermittelt die Ordner, aus denen der Protokoll-Editor Medien zusaetzlich zum
/// aktuellen Projektordner anzeigen darf (Gesamtaudit 2026-08-14, Prio 2).
///
/// Hintergrund: Videos und Fotos liegen oft in externen Kundenordnern. Eine harte
/// Begrenzung auf den Projektordner wuerde sie unsichtbar machen. Erlaubt sind
/// deshalb die konfigurierte Projektwurzel und die Ordner der zuletzt genutzten
/// Projekte — aber keine beliebigen Systempfade.
///
/// Bewusst eine eigene Klasse: Der Dialog soll keine Pfadlogik enthalten
/// (Architekturtest), und der Resolver soll die Einstellungen nicht kennen.
/// </summary>
internal static class ProtocolEntryEditorMediaRoots
{
    public static IReadOnlyList<string> From(AppSettings? settings)
    {
        var roots = new List<string>();
        if (settings is null)
            return roots;

        Add(roots, settings.ProjectsRootDirectory);

        foreach (var projektpfad in settings.RecentProjectPaths ?? new List<string>())
            Add(roots, ProjectRootOf(projektpfad));

        return roots;
    }

    private static string? ProjectRootOf(string? projektpfad)
    {
        if (string.IsNullOrWhiteSpace(projektpfad))
            return null;

        try
        {
            return ProjectFileLocator.ProjectRootFromFile(projektpfad)
                   ?? Path.GetDirectoryName(projektpfad);
        }
        catch (System.Exception ex) when (ex is System.ArgumentException or PathTooLongException)
        {
            // Ein unbrauchbarer Eintrag in der Liste darf den Dialog nicht stoeren.
            return null;
        }
    }

    private static void Add(List<string> roots, string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
            return;
        if (!roots.Contains(pfad, System.StringComparer.OrdinalIgnoreCase))
            roots.Add(pfad);
    }
}
