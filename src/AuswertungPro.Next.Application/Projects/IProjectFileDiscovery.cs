using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Findet gespeicherte Projektdateien unter bekannten Basisordnern.
/// Der Vertrag bleibt frei von konkreten Dateisystem-Implementierungen.
/// </summary>
public interface IProjectFileDiscovery
{
    IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories);
}
