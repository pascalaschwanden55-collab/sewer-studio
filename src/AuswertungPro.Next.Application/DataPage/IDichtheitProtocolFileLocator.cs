using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Findet verteilte Dichtheitspruefungsprotokolle einer Haltung im Projekt
/// und in einer optionalen externen Verteil-Wurzel.
/// </summary>
public interface IDichtheitProtocolFileLocator
{
    IReadOnlyList<string> FindPdfPaths(
        HaltungRecord? record,
        string? projectFolder,
        string? configuredRoot);
}
