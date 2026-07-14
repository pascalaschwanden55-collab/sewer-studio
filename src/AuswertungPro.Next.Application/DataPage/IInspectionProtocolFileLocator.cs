using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Findet und löst Inspektionsprotokoll-PDFs einer Haltung oder eines Schachts auf.
/// </summary>
public interface IInspectionProtocolFileLocator
{
    string? ResolveExistingPath(string? raw, string? projectPath);

    string? FindProtocolPath(
        HaltungRecord record,
        string? resolvedLink,
        string? initialFolder,
        string? projectPath,
        string? storedFilesRaw);

    List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder);

    void AddResolvedPdf(List<string> paths, string? raw, string projectFolder);

    void ResolveSchachtPdfPaths(SchachtRecord schacht, string projectFolder, List<string> paths);
}
