using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis der Neuerzeugung aller programmeigenen Haltungsprotokolle.</summary>
public sealed record ProtocolRegenerationResult(
    int Generated,
    int Errors,
    IReadOnlyList<string> Messages);

/// <summary>Erzeugt die programmeigenen Haltungsprotokolle eines Projekts neu.</summary>
public interface IProtocolRegenerationService
{
    ProtocolRegenerationResult RegenerateAll(
        Project project,
        string projectFolder,
        ICodeCatalogProvider? codeCatalog = null);
}

/// <summary>Erzeugt das programmeigene Protokoll einer einzelnen Haltung neu.</summary>
public interface IProtocolSingleRegenerationService
{
    string? RegenerateOne(
        Project project,
        string projectFolder,
        HaltungRecord record,
        ProtocolDocument document,
        ICodeCatalogProvider? codeCatalog = null);
}
