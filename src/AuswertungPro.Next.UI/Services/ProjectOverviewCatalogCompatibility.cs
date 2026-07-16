using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Uebergangsfabrik fuer bestehende oeffentliche Overview-Konstruktoren.
/// Der normale Programmweg verwendet den registrierten Dienst.
/// </summary>
internal static class ProjectOverviewCatalogCompatibility
{
    internal static IProjectOverviewCatalog Create(IProjectFileDiscovery projectFileDiscovery)
        => new ProjectOverviewCatalogService(projectFileDiscovery);
}
