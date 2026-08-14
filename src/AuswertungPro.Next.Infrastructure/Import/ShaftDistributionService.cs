using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kapselt den alten Schacht-PDF-Splitter und leitet Projektziele in das
/// gemeinsame Import-Staging um. Konfigurierte externe Ziele behalten den
/// bisherigen direkten Exportweg, weil der Projektmarker dort nicht loeschen darf.
/// </summary>
public sealed class ShaftDistributionService : IShaftDistributionService
{
    public ShaftDistributionResult Distribute(ShaftDistributionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Project);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationFolder);

        var progress = request.Progress is null
            ? null
            : new ProgressAdapter(request.Progress);
        if (request.FileStaging is null)
        {
            return new ShaftDistributionResult(
                RunLegacy(request, request.DestinationFolder, progress)
                    .Select(ToDirectItem)
                    .ToList(),
                UsesPersistentProjectTransaction: false);
        }

        EnsureDestinationInsideProject(request.FileStaging, request.DestinationFolder);
        using var output = new StagedDistributionOutput();
        var temporary = RunLegacy(request, output.OutputRoot, progress);
        output.StageAll(request.FileStaging, request.DestinationFolder);
        var items = temporary.Select(result =>
        {
            var target = output.MapPath(result.DestPdfPath, request.DestinationFolder);
            var read = string.IsNullOrWhiteSpace(target)
                ? null
                : request.FileStaging.ResolveReadPath(target);
            return new ShaftDistributionItem(
                result.Success,
                result.Message,
                result.SourcePdfPath,
                target,
                read,
                output.MapPath(result.HoldingFolder, request.DestinationFolder));
        }).ToList();

        return new ShaftDistributionResult(
            items,
            UsesPersistentProjectTransaction: true);
    }

    private static IReadOnlyList<HoldingFolderDistributor.DistributionResult> RunLegacy(
        ShaftDistributionRequest request,
        string destinationFolder,
        IProgress<HoldingFolderDistributor.DistributionProgress>? progress)
    {
        if (request.PdfFiles is { Count: > 0 })
        {
            return HoldingFolderDistributor.DistributeShaftFiles(
                pdfFiles: request.PdfFiles,
                destGemeindeFolder: destinationFolder,
                moveInsteadOfCopy: false,
                overwrite: false,
                project: request.Project,
                progress: progress,
                directoryConfig: request.DirectoryConfig,
                variant: request.Variant);
        }

        if (string.IsNullOrWhiteSpace(request.PdfSourceFolder))
            throw new ArgumentException("PDF-Dateien oder PDF-Quellordner fehlen.", nameof(request));

        return HoldingFolderDistributor.DistributeShafts(
            pdfSourceFolder: request.PdfSourceFolder,
            destGemeindeFolder: destinationFolder,
            moveInsteadOfCopy: false,
            overwrite: false,
            project: request.Project,
            progress: progress,
            directoryConfig: request.DirectoryConfig,
            variant: request.Variant);
    }

    private static ShaftDistributionItem ToDirectItem(
        HoldingFolderDistributor.DistributionResult result)
        => new(
            result.Success,
            result.Message,
            result.SourcePdfPath,
            result.DestPdfPath,
            result.DestPdfPath,
            result.HoldingFolder);

    private static void EnsureDestinationInsideProject(
        IImportFileStagingSession fileStaging,
        string destinationFolder)
    {
        var root = Path.GetFullPath(fileStaging.ProjectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var destination = Path.GetFullPath(destinationFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!destination.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Persistentes Schacht-Staging ist nur innerhalb des Projektordners erlaubt.");
        }
    }

    private sealed class ProgressAdapter(IProgress<ShaftDistributionProgress> target)
        : IProgress<HoldingFolderDistributor.DistributionProgress>
    {
        public void Report(HoldingFolderDistributor.DistributionProgress value)
            => target.Report(new ShaftDistributionProgress(
                value.Processed,
                value.Total,
                value.CurrentFile));
    }
}
