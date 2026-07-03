using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Findet Projektdateien fuer die Projektuebersicht unterhalb der Basisordner.
/// Deckt beide Ablage-Strukturen ab: Alt-Projekte (*.json direkt im Projektordner)
/// und neue Struktur (&lt;Projekt&gt;\Projektdateien\projekt.json). Die Platte ist
/// damit die Wahrheitsquelle der Liste — nicht die Settings-Merkliste.
/// </summary>
public static class ProjectFileDiscovery
{
    public static IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string file)
        {
            if (seen.Add(file))
                result.Add(file);
        }

        foreach (var baseDir in baseDirectories)
        {
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                continue;

            try
            {
                // JSONs direkt im Basisordner (flach abgelegte Alt-Projekte).
                foreach (var file in Directory.GetFiles(baseDir, "*.json"))
                    Add(file);

                foreach (var subDir in Directory.GetDirectories(baseDir))
                {
                    // Alt-Projekte: beliebig benannte *.json im Projektordner-Root.
                    foreach (var file in Directory.GetFiles(subDir, "*.json"))
                        Add(file);

                    // Neue Struktur: <Projekt>\Projektdateien\projekt.json.
                    var located = ProjectFileLocator.Locate(subDir);
                    if (located is not null)
                        Add(located);
                }
            }
            catch
            {
                // Zugriff verweigert o.ae. — Ordner ueberspringen, Liste nicht abbrechen.
            }
        }

        return result;
    }
}
