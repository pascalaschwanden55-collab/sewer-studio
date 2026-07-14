using System.Collections.Generic;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Findet Projektdateien fuer die Projektuebersicht unterhalb der Basisordner.
/// Deckt beide Ablage-Strukturen ab: Alt-Projekte (*.json direkt im Projektordner)
/// und neue Struktur (&lt;Projekt&gt;\Projektdateien\projekt.json). Die Platte ist
/// damit die Wahrheitsquelle der Liste — nicht die Settings-Merkliste.
/// </summary>
public static class ProjectFileDiscovery
{
    private static readonly IProjectFileDiscovery DefaultService = new ProjectFileDiscoveryService();

    internal static IProjectFileDiscovery CompatibilityService => DefaultService;

    public static IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories)
        => DefaultService.FindProjectFiles(baseDirectories);
}
