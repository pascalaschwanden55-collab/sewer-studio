using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest den oeffentlichen Grundbuchauszug einer Liegenschaft. Kennt kein Dossier.
/// </summary>
public interface ILandRegistryLookup
{
    /// <summary>Null, wenn die Auskunft nicht sicher gelesen werden konnte.</summary>
    Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default);
}
