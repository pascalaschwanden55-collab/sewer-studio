using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest Liegenschaften aus dem Parzellendienst. Kennt kein Dossier.
/// </summary>
public interface IParcelLookup
{
    /// <summary>Eine Parzelle ueber Gemeindenummer und Parzellennummer. Null, wenn es sie nicht gibt.</summary>
    Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default);

    /// <summary>Alle Parzellen, die von den uebergebenen WKT-Linien beruehrt werden.</summary>
    Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
        IReadOnlyList<string> wktLines, CancellationToken ct = default);

    /// <summary>Die Gemeinden des Kantons mit ihrer BFS-Nummer.</summary>
    Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default);
}
