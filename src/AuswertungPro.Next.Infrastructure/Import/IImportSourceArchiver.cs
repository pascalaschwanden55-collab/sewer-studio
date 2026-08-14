namespace AuswertungPro.Next.Infrastructure.Import;

using AuswertungPro.Next.Application.Import;

/// <summary>
/// Archiviert relevante Rohdateien eines Kanalfernsehen-Exports im Projekt.
/// </summary>
public interface IImportSourceArchiver
{
    ArchiveResult Archive(string sourceFolder, string projectFolder);

    ArchiveResult Archive(
        string sourceFolder,
        string projectFolder,
        IImportFileStagingSession? fileStaging)
        => Archive(sourceFolder, projectFolder);
}
