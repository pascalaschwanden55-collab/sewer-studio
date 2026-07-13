using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Bindet die bestehende Protokoll-Neuerzeugung an den Application-Vertrag an.</summary>
public sealed class ProtocolRegenerationAdapter : IProtocolRegenerationService
{
    public ProtocolRegenerationResult RegenerateAll(
        Project project,
        string projectFolder,
        ICodeCatalogProvider? codeCatalog = null)
    {
        var result = ProtocolRegenerationService.RegenerateAll(project, projectFolder, codeCatalog);
        return new ProtocolRegenerationResult(
            result.Generated,
            result.Errors,
            result.Messages);
    }
}
