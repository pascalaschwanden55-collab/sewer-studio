namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Archiviert relevante Rohdateien eines Kanalfernsehen-Exports im Projekt.
/// </summary>
public interface IImportSourceArchiver
{
    ArchiveResult Archive(string sourceFolder, string projectFolder);
}
