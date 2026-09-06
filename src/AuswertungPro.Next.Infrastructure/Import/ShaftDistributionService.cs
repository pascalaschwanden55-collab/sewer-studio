using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Common;
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
            if (request.PdfFiles is not { Count: > 0 } && !string.IsNullOrWhiteSpace(request.PdfSourceFolder))
            {
                var skipped = new List<string>();
                var pdfs = SafeFileEnumeration.EnumerateFilesSafe(request.PdfSourceFolder, "*.pdf", skippedDirectories: skipped).ToList();
                if (pdfs.Count == 0 && skipped.Count == 0)
                    return new ShaftDistributionResult([], false);
            }
            return new ShaftDistributionResult(
                RunLegacy(request, request.DestinationFolder, progress)
                    .Select(ToDirectItem)
                    .ToList(),
                UsesPersistentProjectTransaction: false);
        }

        EnsureDestinationInsideProject(request.FileStaging, request.DestinationFolder);
        using var output = new StagedDistributionOutput();
        var sources = request.PdfFiles is { Count: > 0 }
            ? request.PdfFiles.Select(p => new ImportReadableFile(p, p)).ToList()
            : request.FileStaging.EnumerateReadableFiles(
                request.PdfSourceFolder ?? throw new ArgumentException("PDF-Quellordner fehlt."),
                "*.pdf", SearchOption.AllDirectories);
        var temporary = new List<HoldingFolderDistributor.DistributionResult>();
        var logicalSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            try
            {
                var readPath = source.ReadPath;
                if (new ImportFileStagingPathGuard(request.FileStaging.ProjectRoot).IsWithinProject(source.TargetPath))
                    readPath = request.FileStaging.ResolveReadPath(source.TargetPath);
                else if (!ImportSourcePathGuard.TryInspectFile(readPath, out readPath, out var exists, out var error) || !exists)
                    throw new IOException(error ?? "Quelldatei fehlt.");
                var readable = output.CreateReadableCopy(readPath, Path.GetFileName(source.TargetPath));
                logicalSources[readable] = source.TargetPath;
                temporary.AddRange(RunLegacy(request with { PdfFiles = [readable] }, output.OutputRoot, progress));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                temporary.Add(new HoldingFolderDistributor.DistributionResult(
                    false, $"Schachtprotokoll nicht lesbar: {ex.Message}", source.TargetPath,
                    null, null, null, null, null, HoldingFolderDistributor.VideoMatchStatus.NotChecked));
            }
        }
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
                logicalSources.GetValueOrDefault(result.SourcePdfPath, result.SourcePdfPath),
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
        var skipped = new List<string>();
        var candidates = request.PdfFiles is { Count: > 0 }
            ? request.PdfFiles
            : !string.IsNullOrWhiteSpace(request.PdfSourceFolder)
                ? SafeFileEnumeration.EnumerateFilesSafe(request.PdfSourceFolder, "*.pdf", skippedDirectories: skipped).ToList()
                : throw new ArgumentException("PDF-Dateien oder PDF-Quellordner fehlen.", nameof(request));
        var selected = candidates.Where(ShaftPdfRelevance.ShouldProcess).ToList();
        var results = new List<HoldingFolderDistributor.DistributionResult>();
        if (selected.Count > 0)
            results.AddRange(HoldingFolderDistributor.DistributeShaftFiles(
                pdfFiles: selected,
                destGemeindeFolder: destinationFolder,
                moveInsteadOfCopy: false,
                overwrite: false,
                project: request.Project,
                progress: progress,
                directoryConfig: request.DirectoryConfig,
                variant: request.Variant));
        foreach (var path in skipped)
            results.Add(new HoldingFolderDistributor.DistributionResult(false,
                "PDF-Quellordner nicht lesbar: " + path, path, null, null, null, null, null,
                HoldingFolderDistributor.VideoMatchStatus.NotChecked));
        return results;
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
