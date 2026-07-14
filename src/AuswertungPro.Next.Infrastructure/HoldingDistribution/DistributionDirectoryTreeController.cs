using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Verbindet die reine Baum-Aufloesung mit Projektmetadaten. Der feste letzte
/// Objektordner bleibt immer erhalten, weil Video- und Projektlogik darauf aufbauen.
/// </summary>
internal static class DistributionDirectoryTreeController
{
    private static readonly IDistributionDirectoryTreeResolver Resolver =
        new DistributionDirectoryTreeResolver();

    internal static string ResolveObjectFolder(
        string destinationRoot,
        DistributionTargetConfig? directoryConfig,
        DistributionPatternContext context,
        string fixedObjectFolderPattern)
        => Resolver.ResolveObjectDirectory(
            destinationRoot,
            directoryConfig?.OrdnerPattern,
            directoryConfig?.UnterordnerPattern,
            fixedObjectFolderPattern,
            context);

    internal static string? GetMunicipality(Project? project)
        => project?.Metadata.TryGetValue("Gemeinde", out var value) == true
            && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
}
