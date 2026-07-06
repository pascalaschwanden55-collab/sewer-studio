using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerPreparation
{
    public static void EnsureCurrentFolders(string projectFolder, bool dryRun, ModernizeReport report)
    {
        var folders = new[]
        {
            Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.Datenbanken),
            Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.XtfDir),
            Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.PdfDir),
            Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.TxtDir),
            Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt),
            Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt),
            Path.Combine(projectFolder, ProjectStructure.Plaene),
            Path.Combine(projectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen),
            Path.Combine(projectFolder, ProjectStructure.Fotos, ProjectStructure.FotosSchaechte),
            Path.Combine(projectFolder, ProjectStructure.Projektdateien),
            Path.Combine(projectFolder, ProjectStructure.ImportReports),
            Path.Combine(projectFolder, ProjectStructure.RestorePoints),
        };

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder))
                continue;
            if (!dryRun)
                Directory.CreateDirectory(folder);
            report.FoldersCreated++;
        }
    }

    public static void CreateProjectBackup(string projectFile, string projectFolder, ModernizeReport report)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(projectFolder, ProjectStructure.RestorePoints, "modernize", stamp);
        Directory.CreateDirectory(backupDir);
        var dest = Path.Combine(backupDir, Path.GetFileName(projectFile));
        File.Copy(projectFile, dest, overwrite: false);
        report.Messages.Add($"Projektdatei-Backup: {dest}");
    }

    public static void CopyLegacyImports(string projectFolder, bool dryRun, ModernizeReport report)
    {
        var legacy = Path.Combine(projectFolder, ModernizerLegacyFolders.Imports);
        if (!Directory.Exists(legacy))
            return;

        foreach (var source in ModernizerFileSystem.EnumerateFilesSafe(legacy))
        {
            var ext = Path.GetExtension(source).ToLowerInvariant();
            var sub = ext switch
            {
                ".pdf" => ProjectStructure.PdfDir,
                ".xtf" or ".xml" => ProjectStructure.XtfDir,
                ".mdb" or ".accdb" or ".db3" or ".fdb" => ProjectStructure.Datenbanken,
                ".txt" or ".csv" => ProjectStructure.TxtDir,
                _ => null
            };

            if (sub is null)
            {
                report.ImportSkipped++;
                continue;
            }

            var targetDir = ProjectStructure.ImportdateienDir(projectFolder, sub);
            ModernizerFileSystem.CopyFileToDirectory(source, targetDir, dryRun, report, FileCopyKind.Import);
        }
    }

    public static void CopyPlanFolders(string projectFolder, string? sourceFolder, bool dryRun, ModernizeReport report)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return;

        var planFiles = ModernizerFileSystem.EnumerateFilesSafe(sourceFolder)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(seg => seg.Contains("Plan", StringComparison.OrdinalIgnoreCase)
                            || seg.Contains("Pl\u00e4n", StringComparison.OrdinalIgnoreCase)
                            || seg.Contains("Plaen", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var targetDir = ProjectStructure.PlaeneDir(projectFolder);
        foreach (var plan in planFiles)
            ModernizerFileSystem.CopyFileToDirectory(plan, targetDir, dryRun, report, FileCopyKind.Plan);
    }

    public static void CopyLegacyTree(
        string projectFolder,
        string legacyFolderName,
        string targetFolderName,
        bool dryRun,
        ModernizeReport report)
    {
        var legacyRoot = Path.Combine(projectFolder, legacyFolderName);
        if (!Directory.Exists(legacyRoot))
            return;

        var targetRoot = Path.Combine(projectFolder, targetFolderName);
        foreach (var source in ModernizerFileSystem.EnumerateFilesSafe(legacyRoot))
        {
            var relative = Path.GetRelativePath(legacyRoot, source);
            var target = Path.Combine(targetRoot, relative);
            ModernizerFileSystem.CopyFileExact(
                source,
                target,
                dryRun,
                report,
                legacyFolderName.StartsWith("Sch", StringComparison.OrdinalIgnoreCase)
                    ? FileCopyKind.Schacht
                    : FileCopyKind.Haltung);
        }
    }

    public static void CopyTopLevelFotos(string projectFolder, bool dryRun, ModernizeReport report)
    {
        var root = Path.Combine(projectFolder, ProjectStructure.Fotos);
        if (!Directory.Exists(root))
            return;

        var targetDir = Path.Combine(root, ProjectStructure.FotosHaltungen, "_ALT_ROOT");
        foreach (var source in Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (MediaFileTypes.HasImageExtension(source))
                ModernizerFileSystem.CopyFileToDirectory(source, targetDir, dryRun, report, FileCopyKind.Photo);
        }
    }

}
