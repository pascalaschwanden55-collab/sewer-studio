using System.Globalization;
using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportPostProcessingRequest(
    string SourceFolder,
    string SourceLabel,
    Project Project,
    string? ProjectFolder,
    IPdfImportService PdfImport,
    IImportMediaDistributionService MediaDistribution,
    string? PdfToTextPath,
    bool FillMissingOnly,
    ImportRunContext? Context,
    object? CollectionLock);

internal sealed record ImportPostProcessingActions(
    Action<string> SetProgressText,
    Action<double> SetProgressPercent,
    Action<string> AppendSummaryText,
    Action<string> AppendDetailsText,
    Action<string> SetStatus);

internal static class ImportPostProcessingController
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] PdfDirectories = ["Report", "Reports", "PDF", "Dokumente"];

    internal static async Task RunAsync(
        ImportPostProcessingRequest request,
        ImportPostProcessingActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        TrackImportSource(request.Project, request.SourceFolder, request.SourceLabel, DateTime.Now);
        await ImportPdfsAsync(request, actions);
        await DistributeMediaAsync(request, actions);
    }

    internal static void TrackImportSource(
        Project project,
        string sourcePath,
        string importType,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(project);

        var entry = $"{timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} | {importType} | {sourcePath}";
        project.Metadata["ImportQuelle"] = sourcePath;
        project.Metadata["ImportQuellTyp"] = importType;

        const string historyKey = "ImportQuellenHistorie";
        var existing = project.Metadata.TryGetValue(historyKey, out var history) ? history : "";
        var lines = existing.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        lines.Add(entry);
        if (lines.Count > 20)
            lines = lines.Skip(lines.Count - 20).ToList();

        project.Metadata[historyKey] = string.Join("\n", lines);
    }

    private static async Task ImportPdfsAsync(
        ImportPostProcessingRequest request,
        ImportPostProcessingActions actions)
    {
        actions.SetProgressText($"{request.SourceLabel}: PDF-Protokolle werden gelesen...");

        var result = await Task.Run(() =>
        {
            var pdfFiles = EnumerateProjectFiles(
                    request.SourceFolder,
                    PdfExtensions,
                    includeRoot: true,
                    PdfDirectories)
                .ToArray();

            if (pdfFiles.Length == 0)
                return new PdfScanResult(0, 0, 0, "Keine PDF-Dateien im Quellordner gefunden.");

            var found = 0;
            var updated = 0;
            var errors = 0;

            for (var i = 0; i < pdfFiles.Length; i++)
            {
                request.Context?.CancellationToken.ThrowIfCancellationRequested();
                var path = pdfFiles[i];
                request.Context?.Progress?.Report(new ImportProgress(
                    "PDF-Scan",
                    i + 1,
                    pdfFiles.Length,
                    $"PDF {i + 1}/{pdfFiles.Length}",
                    Path.GetFileName(path)));

                try
                {
                    var import = request.PdfImport.ImportPdf(
                        path,
                        request.Project,
                        request.PdfToTextPath,
                        request.FillMissingOnly,
                        request.Context);
                    if (import.Ok && import.Value is not null)
                    {
                        found += import.Value.Found;
                        updated += import.Value.Updated;
                    }
                    else
                    {
                        errors++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    errors++;
                }
            }

            var message = $"PDF-Scan: {pdfFiles.Length} Dateien, {found} Haltungen zugeordnet, {updated} aktualisiert, {errors} Fehler";
            return new PdfScanResult(pdfFiles.Length, found, updated, message);
        });

        actions.AppendSummaryText($"\n{result.Message}");
        if (result.Files > 0)
            actions.AppendDetailsText($"\n\n{result.Message}");
    }

    private static async Task DistributeMediaAsync(
        ImportPostProcessingRequest request,
        ImportPostProcessingActions actions)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectFolder))
        {
            actions.AppendDetailsText("\nHinweis: Projekt bitte speichern, um Medien im Projektordner abzulegen.");
            return;
        }

        var holdingCount = request.Project.Data.Count;
        if (holdingCount == 0)
        {
            actions.AppendDetailsText($"\n{request.SourceLabel}: Keine Haltungen im Projekt - Medienverteilung uebersprungen.");
            return;
        }

        actions.SetProgressText(
            $"{request.SourceLabel}: Fotos/PDFs von {holdingCount} Haltungen werden in Projektordner kopiert (Videos erst beim Verteilen)...");
        var progress = new Progress<ImportMediaDistributionProgress>(value =>
        {
            actions.SetProgressText($"Kopiere: {value.Processed}/{value.Total} ({value.CurrentFile})");
            if (value.Total > 0)
                actions.SetProgressPercent((double)value.Processed / value.Total * 100.0);
        });

        var cancellationToken = request.Context?.CancellationToken ?? CancellationToken.None;
        var dryRun = request.Context?.DryRun ?? false;
        var distribution = await Task.Run(() =>
            request.MediaDistribution.Distribute(
                new ImportMediaDistributionRequest(
                    ProjectFolder: request.ProjectFolder,
                    Project: request.Project,
                    Progress: progress,
                    CancellationToken: cancellationToken,
                    DryRun: dryRun,
                    CollectionLock: request.CollectionLock,
                    IncludeVideos: false,
                    FileStaging: request.Context?.FileStaging)));

        actions.AppendSummaryText(
            $"\nMedien-Verteilung ({holdingCount} Haltungen):\n  {distribution.FilesCopied} Dateien kopiert\n  {distribution.FilesSkipped} uebersprungen\n  {distribution.Errors} Fehler");
        if (distribution.Messages.Count > 0)
        {
            actions.AppendDetailsText(
                "\n\nMedien-Details:\n" + string.Join("\n", distribution.Messages.Take(50)));
        }

        actions.SetStatus($"{request.SourceLabel}-Projekt importiert und verteilt");
    }

    private static IEnumerable<string> EnumerateProjectFiles(
        string root,
        IReadOnlyCollection<string> extensions,
        bool includeRoot,
        IReadOnlyCollection<string> includeDirs)
    {
        var searched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yieldedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (includeRoot && Directory.Exists(root))
            searched.Add(root);

        foreach (var dir in includeDirs)
        {
            var full = Path.Combine(root, dir);
            if (Directory.Exists(full))
                searched.Add(full);
        }

        if (searched.Count == 0)
            searched.Add(root);

        foreach (var baseDir in searched)
        {
            IEnumerable<string> files;
            try
            {
                files = SafeFileEnumeration.EnumerateFilesSafe(baseDir, "*.*", recursive: true);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                    && yieldedFiles.Add(file))
                    yield return file;
            }
        }
    }

    private sealed record PdfScanResult(int Files, int Found, int Updated, string Message);
}
