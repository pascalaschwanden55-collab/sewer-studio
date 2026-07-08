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
/// </summary>
public interface INameBasedProtocolDistributor
{
    ProtocolDistributionReport Distribute(Project project, string projectFolder, string sourceFolder);
}
