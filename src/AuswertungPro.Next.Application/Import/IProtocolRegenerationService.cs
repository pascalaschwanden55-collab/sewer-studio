using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

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
