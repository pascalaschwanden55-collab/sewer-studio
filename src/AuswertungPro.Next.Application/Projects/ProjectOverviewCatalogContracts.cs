using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Eingaben fuer die Projektliste der Uebersicht. Die Suchwurzeln werden vom
/// Aufrufer bestimmt; der Katalog kuemmert sich nur um Lesen und Sortieren.
/// </summary>
public sealed record ProjectOverviewCatalogRequest(
    string? LastProjectPath,
    IReadOnlyList<string> RecentProjectPaths,
    IReadOnlyList<string> HiddenProjectPaths,
    IReadOnlyList<string> ScanRoots);

/// <summary>UI-unabhaengige Beschreibung einer gespeicherten Projektdatei.</summary>
public sealed record ProjectOverviewDescriptor(
    string Name,
    string Description,
    string Path,
    DateTime? ModifiedAtUtc,
    bool IsLastProject,
    int HoldingCount,
    int SchachtCount,
    bool IsCorrupt);

/// <summary>Liest die bekannten Projektdateien fuer die Projekt-Uebersicht.</summary>
public interface IProjectOverviewCatalog
{
    IReadOnlyList<ProjectOverviewDescriptor> Load(ProjectOverviewCatalogRequest request);
}
