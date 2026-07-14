using System.Collections.Generic;
using System.IO;

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
}

public sealed class PlanPdfImportService : IPlanPdfImporter
{
    private readonly Func<string, bool> _looksLikePlan;

    public PlanPdfImportService(Func<string, bool>? looksLikePlan = null)
        => _looksLikePlan = looksLikePlan ?? DefaultLooksLikePlan;

    public PlanPdfImportResult ImportFromArchivedPdfFolder(string archivedPdfDir, string projectFolder)
    {
        var copied = 0;
        var reused = 0;
        var skipped = 0;
        var errors = 0;
        var messages = new List<string>();

        if (!Directory.Exists(archivedPdfDir))
            return new PlanPdfImportResult(copied, reused, skipped, errors, messages);

        var plaeneDir = ProjectStructure.PlaeneDir(projectFolder);
        Directory.CreateDirectory(plaeneDir);

        foreach (var sourcePath in Directory.EnumerateFiles(archivedPdfDir, "*.pdf", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (!_looksLikePlan(sourcePath))
                {
                    skipped++;
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(plaeneDir, fileName);

                if (File.Exists(targetPath))
                {
                    var sourceSize = new FileInfo(sourcePath).Length;
                    var targetSize = new FileInfo(targetPath).Length;
                    if (sourceSize == targetSize)
                    {
                        reused++;
                        continue;
                    }

                    targetPath = BuildCollisionSafePath(plaeneDir, fileName);
                    messages.Add($"Plan-Namenskollision: '{fileName}' als '{Path.GetFileName(targetPath)}' kopiert.");
                }

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
