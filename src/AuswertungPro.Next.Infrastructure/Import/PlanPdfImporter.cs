using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

public sealed record PlanPdfImportResult(
    int Copied,
    int Reused,
    int Skipped,
    int Errors,
    IReadOnlyList<string> Messages);

/// <summary>
/// Kopiert erkannte Plan-PDFs aus dem Rohdatenarchiv in den zentralen Projektordner Pläne.
/// </summary>
public interface IPlanPdfImporter
{
    PlanPdfImportResult ImportFromArchivedPdfFolder(string archivedPdfDir, string projectFolder);

    PlanPdfImportResult ImportFromArchivedPdfFolder(
        string archivedPdfDir,
        string projectFolder,
        IImportFileStagingSession? fileStaging)
        => ImportFromArchivedPdfFolder(archivedPdfDir, projectFolder);
}

public sealed class PlanPdfImportService : IPlanPdfImporter
{
    private readonly Func<string, bool> _looksLikePlan;

    public PlanPdfImportService(Func<string, bool>? looksLikePlan = null)
        => _looksLikePlan = looksLikePlan ?? DefaultLooksLikePlan;

    public PlanPdfImportResult ImportFromArchivedPdfFolder(string archivedPdfDir, string projectFolder)
        => ImportFromArchivedPdfFolder(archivedPdfDir, projectFolder, fileStaging: null);

    public PlanPdfImportResult ImportFromArchivedPdfFolder(
        string archivedPdfDir,
        string projectFolder,
        IImportFileStagingSession? fileStaging)
    {
        var copied = 0;
        var reused = 0;
        var skipped = 0;
        var errors = 0;
        var messages = new List<string>();

        var archivedPdfs = fileStaging is null
            ? Directory.Exists(archivedPdfDir)
                ? Directory.EnumerateFiles(archivedPdfDir, "*.pdf", SearchOption.TopDirectoryOnly)
                    .Select(path => new ImportReadableFile(path, path))
                    .ToList()
                : []
            : fileStaging.EnumerateReadableFiles(
                archivedPdfDir,
                "*.pdf",
                SearchOption.TopDirectoryOnly);
        if (archivedPdfs.Count == 0)
            return new PlanPdfImportResult(copied, reused, skipped, errors, messages);

        var plaeneDir = ProjectStructure.PlaeneDir(projectFolder);
        var writePathGuard = fileStaging is null
            ? new ProjectWritePathGuard(projectFolder)
            : null;

        foreach (var source in archivedPdfs)
        {
            var sourcePath = source.ReadPath;
            try
            {
                if (!_looksLikePlan(sourcePath))
                {
                    skipped++;
                    continue;
                }

                var fileName = Path.GetFileName(source.TargetPath);
                var targetPath = Path.Combine(plaeneDir, fileName);

                if (fileStaging is not null)
                {
                    var before = fileStaging.PreparedFiles.Count;
                    targetPath = fileStaging.StageCopyAs(
                        sourcePath,
                        plaeneDir,
                        fileName);
                    if (fileStaging.PreparedFiles.Count == before)
                        reused++;
                    else
                        copied++;
                    if (!fileName.Equals(Path.GetFileName(targetPath), StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(
                            $"Plan-Namenskollision: '{fileName}' als '{Path.GetFileName(targetPath)}' vorbereitet.");
                    }
                    continue;
                }

                var safePlansDirectory = writePathGuard!.EnsureSafeDirectoryTarget(plaeneDir);
                Directory.CreateDirectory(safePlansDirectory);
                targetPath = writePathGuard.EnsureSafeFileTarget(
                    Path.Combine(safePlansDirectory, fileName));

                if (File.Exists(targetPath))
                {
                    if (VerifiedImportFileCopy.ContentsEqual(sourcePath, targetPath))
                    {
                        reused++;
                        continue;
                    }

                    targetPath = BuildCollisionSafePath(safePlansDirectory, fileName);
                    messages.Add($"Plan-Namenskollision: '{fileName}' als '{Path.GetFileName(targetPath)}' kopiert.");
                }

                targetPath = writePathGuard.EnsureSafeFileTarget(targetPath);
                File.Copy(sourcePath, targetPath, overwrite: false);
                copied++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Plan-PDF nicht importiert: {Path.GetFileName(sourcePath)} - {ex.Message}");
            }
        }

        return new PlanPdfImportResult(copied, reused, skipped, errors, messages);
    }

    internal static bool DefaultLooksLikePlan(string path)
        => PdfDokumentTypErkennung.ErkenneDatei(path) == PdfDokumentTyp.PlanSituation;

    private static string BuildCollisionSafePath(string directory, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{stem}_{counter}{extension}");
            counter++;
        }
        while (File.Exists(candidate));

        return candidate;
    }
}

/// <summary>Kompatible statische Fassade fuer bestehende Aufrufer.</summary>
public static class PlanPdfImporter
{
    private static readonly IPlanPdfImporter Default = new PlanPdfImportService();

    public static PlanPdfImportResult ImportFromArchivedPdfFolder(string archivedPdfDir, string projectFolder)
        => Default.ImportFromArchivedPdfFolder(archivedPdfDir, projectFolder);

    internal static bool LooksLikePlan(string path)
        => PlanPdfImportService.DefaultLooksLikePlan(path);
}
