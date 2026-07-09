using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Baut aus einem geladenen <see cref="Project"/> eine <see cref="ProjectPreview"/> fuer die
/// Projektuebersicht. Reiner Helfer (keine Abhaengigkeiten), damit unit-testbar. Kennzahlen kommen
/// aus <see cref="DashboardStatisticsBuilder"/>.
/// </summary>
public static class ProjectPreviewFactory
{
    public static ProjectPreview FromProject(
        Project project,
        string path,
        ProjectCostStore? haltungCosts = null,
        ProjectCostStore? schachtCosts = null)
    {
        var stats = DashboardStatisticsBuilder.Build(project, haltungCosts, schachtCosts);
        return new ProjectPreview(
            Name: project.Name ?? string.Empty,
            Description: project.Description ?? string.Empty,
            Path: path,
            ModifiedAtUtc: project.ModifiedAtUtc,
            HoldingCount: stats.HoldingCount,
            SchachtCount: stats.SchachtCount,
            TotalLengthMeters: stats.TotalLengthMeters,
            TotalCost: stats.TotalCost,
            Auftraggeber: Meta(project, "Auftraggeber"),
            Gemeinde: Meta(project, "Gemeinde"),
            Zone: Meta(project, "Zone"),
            Strasse: Meta(project, "Strasse"),
            Bearbeiter: Meta(project, "Bearbeiter"),
            Inspektionsdatum: Meta(project, "InspektionsDatum"),
            AuftragNr: Meta(project, "AuftragNr"),
            Firma: Meta(project, "FirmaName"),
            Statistics: stats);
    }

    private static string Meta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
}
