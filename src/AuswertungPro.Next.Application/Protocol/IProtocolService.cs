using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public interface IProtocolService
{
    ProtocolDocument EnsureProtocol(string haltungId, IEnumerable<ProtocolEntry> importedEntries, string? user);
    ProtocolRevision StartNachprotokoll(ProtocolDocument doc, string? user, string? comment);
    ProtocolRevision StartNeuProtokoll(ProtocolDocument doc, string? user, string? comment);
    void RestoreOriginal(ProtocolDocument doc, string? user);
    void RestoreRevision(ProtocolDocument doc, ProtocolRevision revision, string? user, string? comment);
}
