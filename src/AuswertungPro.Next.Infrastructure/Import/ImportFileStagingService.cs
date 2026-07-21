using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erzeugt eine isolierte Datei-Sitzung fuer einen gespeicherten Importlauf.
/// </summary>
public sealed class ImportFileStagingService : IImportFileStagingService
{
    public IImportFileStagingSession? Begin(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        var fullProjectPath = Path.GetFullPath(projectPath.Trim());
        var projectRoot = ProjectFileLocator.ProjectRootFromFile(fullProjectPath);
        var projectFileDirectory = Path.GetDirectoryName(fullProjectPath);
        if (string.IsNullOrWhiteSpace(projectRoot)
            || string.IsNullOrWhiteSpace(projectFileDirectory))
        {
            return null;
        }

        return new ImportFileStagingSession(projectRoot, projectFileDirectory);
    }
}
