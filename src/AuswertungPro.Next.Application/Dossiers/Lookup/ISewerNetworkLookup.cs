using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest das Abwassernetz des Kantons. Kennt kein Dossier.
/// </summary>
public interface ISewerNetworkLookup
{
    /// <summary>Lage der genannten Haltungen. Nicht gefundene fehlen im Ergebnis.</summary>
    Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
        IReadOnlyList<string> names, CancellationToken ct = default);

    /// <summary>Alle Haltungen, die auf der Parzelle liegen.</summary>
    Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
        ParcelInfo parcel, CancellationToken ct = default);
}
