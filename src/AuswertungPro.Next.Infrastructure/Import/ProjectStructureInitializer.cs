using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Legt alle verbindlichen Projektordner idempotent an.
/// </summary>
public sealed class ProjectStructureInitializer : IProjectStructureInitializer
{
    private readonly object _sync = new();

    public void EnsureCreated(string projectFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);

        lock (_sync)
        {
            var writePaths = new ProjectWritePathGuard(projectFolder);
            writePaths.EnsureSafeDirectoryTarget(projectFolder);
            var directories = RequiredDirectories(projectFolder)
                .Select(writePaths.EnsureSafeDirectoryTarget)
                .ToArray();

            foreach (var directory in directories)
            {
                writePaths.EnsureSafeDirectoryTarget(directory);
                Directory.CreateDirectory(directory);
            }
        }
    }

    private static IEnumerable<string> RequiredDirectories(string projectFolder)
    {
        yield return Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.Datenbanken);
        yield return Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.XtfDir);
        yield return Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.PdfDir);
        yield return Path.Combine(projectFolder, ProjectStructure.Importdateien, ProjectStructure.TxtDir);
        yield return Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
        yield return Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt);
        yield return Path.Combine(projectFolder, ProjectStructure.Plaene);
        yield return Path.Combine(projectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen);
        yield return Path.Combine(projectFolder, ProjectStructure.Fotos, ProjectStructure.FotosSchaechte);
        yield return Path.Combine(projectFolder, ProjectStructure.Projektdateien);
        yield return Path.Combine(projectFolder, ProjectStructure.ImportReports);
        yield return Path.Combine(projectFolder, ProjectStructure.RestorePoints);
    }
}
