using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>Ergebnis einer name-basierten Protokoll-Verteilung.</summary>
public sealed record ProtocolDistributionReport(
    int HaltungProtokolle,
    int SchachtProtokolle,
    int SchaechteAngelegt,
    IReadOnlyList<string> NichtZugeordnet,
    IReadOnlyList<string> Meldungen);

/// <summary>
/// Verteilt Protokoll-PDFs aus einem Quellordner name-basiert auf Haltungen und Schächte des Projekts.
/// <paramref name="collectionLock"/>: Sync-Objekt der an die UI gebundenen Collections
/// (<c>EnableCollectionSynchronization</c>). Beim Aufruf vom Hintergrund-Thread den
/// <c>ShellViewModel.CollectionLock</c> übergeben, damit das Anlegen neuer Schächte thread-sicher ist.
/// </summary>
public interface INameBasedProtocolDistributor
{
    ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder, object? collectionLock = null);
}
