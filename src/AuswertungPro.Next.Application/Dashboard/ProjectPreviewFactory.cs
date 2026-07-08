using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Baut aus einem geladenen <see cref="Project"/> eine <see cref="ProjectPreview"/> für die
/// Projektübersicht. Reiner Helfer (keine Abhängigkeiten), damit unit-testbar. Kennzahlen kommen
/// aus <see cref="DashboardStatisticsBuilder"/>; Schadensgruppen werden bewusst weggelassen.
/// </summary>
public static class ProjectPreviewFactory
{
    public static ProjectPreview FromProject(Project project, string path)
    {
        var stats = DashboardStatisticsBuilder.Build(project.Data);
        return new ProjectPreview(
            Name: project.Name ?? string.Empty,
            Description: project.Description ?? string.Empty,
            Path: path,
            ModifiedAtUtc: project.ModifiedAtUtc,
            HoldingCount: stats.TotalHoldings,
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
            ConditionClasses: stats.ConditionClasses,
            DnCostGroups: stats.DnCostGroups);
    }

    private static string Meta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
}
