using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Durchsucht bekannte Basisordner nach alten und aktuellen SewerStudio-Projektdateien.
/// Ein nicht lesbarer Ordner wird protokolliert und blockiert die restliche Suche nicht.
/// </summary>
public sealed class ProjectFileDiscoveryService : IProjectFileDiscovery
{
    public IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string file)
        {
            if (seen.Add(file))
                result.Add(file);
        }

        foreach (var baseDirectory in baseDirectories)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
                continue;

            try
            {
                foreach (var file in Directory.GetFiles(baseDirectory, "*.json"))
                    Add(file);

                foreach (var projectDirectory in Directory.GetDirectories(baseDirectory))
                {
                    foreach (var file in Directory.GetFiles(projectDirectory, "*.json"))
                        Add(file);

                    var located = ProjectFileLocator.Locate(projectDirectory);
                    if (located is not null)
                        Add(located);
                }
            }
            catch (Exception ex)
            {
                BestEffort.ReportWarning(
                    $"Projektdateisuche in '{baseDirectory}' uebersprungen: "
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        return result;
    }
}
