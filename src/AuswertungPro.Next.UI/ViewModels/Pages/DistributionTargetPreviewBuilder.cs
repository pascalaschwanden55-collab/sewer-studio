using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

internal sealed record DistributionTargetPreviewRequest(
    string? Root,
    string OrdnerPattern,
    string UnterordnerPattern,
    string DateiPattern,
    string? FixedPattern,
    string? FixedObjectFolderPattern,
    string Extension,
    bool ShowFilePattern,
    bool SupportsSanierung,
    DistributionVariant PreviewVariant,
    DistributionPatternContext SampleContext);

internal sealed record DistributionTargetPreviewResult(
    string Vorschau,
    IReadOnlyList<DistributionTreeNode> TreeNodes);

/// <summary>
/// Baut die reine Pfad- und Ordnerbaum-Vorschau einer Export-Zielkarte.
/// Speichern, Befehle und ViewModel-Zustand bleiben ausserhalb dieses Rechners.
/// </summary>
internal static class DistributionTargetPreviewBuilder
{
    public static DistributionTargetPreviewResult Build(
        DistributionTargetPreviewRequest request,
        IDistributionPatternResolver resolver,
        IDistributionDirectoryTreeResolver directoryTreeResolver)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(directoryTreeResolver);
        ArgumentNullException.ThrowIfNull(request.SampleContext);

        var wurzel = string.IsNullOrWhiteSpace(request.Root)
            ? "<Ziel-Wurzel>"
            : request.Root;

        if (request.ShowFilePattern)
        {
            var relativ = resolver.ResolveRelativePath(
                ordnerPattern: null,
                unterordnerPattern: null,
                dateiPattern: request.DateiPattern,
                context: request.SampleContext,
                extension: request.Extension);
            return new DistributionTargetPreviewResult(
                Path.Combine(wurzel, relativ),
                Array.Empty<DistributionTreeNode>());
        }

        var objektPattern = ResolveObjectPattern(request);
        var variant = request.SupportsSanierung
            ? request.PreviewVariant
            : DistributionVariant.Normal;
        var objektOrdner = directoryTreeResolver.ResolveObjectDirectory(
            wurzel,
            request.OrdnerPattern,
            request.UnterordnerPattern,
            objektPattern,
            request.SampleContext,
            variant,
            request.FixedPattern);
        var datei = resolver.ResolveRelativePath(
            ordnerPattern: null,
            unterordnerPattern: null,
            dateiPattern: request.FixedPattern ?? request.DateiPattern,
            context: request.SampleContext,
            extension: request.Extension);

        return new DistributionTargetPreviewResult(
            Path.Combine(objektOrdner, datei),
            BuildTreeNodes(request, resolver, objektPattern, datei));
    }

    private static IReadOnlyList<DistributionTreeNode> BuildTreeNodes(
        DistributionTargetPreviewRequest request,
        IDistributionPatternResolver resolver,
        string objektPattern,
        string resolvedFileName)
    {
        var nodes = new List<DistributionTreeNode>();
        var depth = 0;

        var ordner = resolver.ResolveSegment(request.OrdnerPattern, request.SampleContext);
        if (!string.IsNullOrWhiteSpace(ordner))
        {
            nodes.Add(new DistributionTreeNode(
                ProjectPathResolver.SanitizePathSegment(ordner),
                DistributionTreeNodeKind.Ordner,
                depth++));
        }

        var unterordner = resolver.ResolveSegment(
            request.UnterordnerPattern,
            request.SampleContext);
        if (!string.IsNullOrWhiteSpace(unterordner))
        {
            nodes.Add(new DistributionTreeNode(
                ProjectPathResolver.SanitizePathSegment(unterordner),
                DistributionTreeNodeKind.Ordner,
                depth++));
        }

        var objekt = resolver.ResolveSegment(objektPattern, request.SampleContext);
        nodes.Add(new DistributionTreeNode(
            ProjectPathResolver.SanitizePathSegment(objekt),
            DistributionTreeNodeKind.Ordner,
            depth++));

        if (request.SupportsSanierung
            && request.PreviewVariant == DistributionVariant.Sanierung)
        {
            var basis = resolver.ResolveSegment(request.FixedPattern, request.SampleContext);
            var jahr = resolver.ResolveSegment("{Jahr}", request.SampleContext);
            var sanierung = DistributionSanierungFolderName.Build(basis, jahr);
            nodes.Add(new DistributionTreeNode(
                ProjectPathResolver.SanitizePathSegment(sanierung),
                DistributionTreeNodeKind.Ordner,
                depth++));
        }

        var datei = Path.GetFileName(resolvedFileName);
        nodes.Add(new DistributionTreeNode(
            datei,
            DistributionTreeNodeKind.Pdf,
            depth));

        var istSchacht = !string.IsNullOrWhiteSpace(request.SampleContext.Schachtnummer);
        var istDp = request.FixedPattern?.EndsWith("_DP", StringComparison.Ordinal) ?? false;
        if (!istSchacht && !istDp)
        {
            nodes.Add(new DistributionTreeNode(
                Path.GetFileNameWithoutExtension(datei) + " (Video)",
                DistributionTreeNodeKind.Video,
                depth));
        }

        return nodes;
    }

    private static string ResolveObjectPattern(DistributionTargetPreviewRequest request)
        => string.IsNullOrWhiteSpace(request.FixedObjectFolderPattern)
            ? string.IsNullOrWhiteSpace(request.SampleContext.Schachtnummer)
                ? "{Haltung}"
                : "{Schachtnummer}"
            : request.FixedObjectFolderPattern;
}
