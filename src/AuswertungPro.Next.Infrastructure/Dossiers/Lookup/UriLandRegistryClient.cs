using System;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest die oeffentliche Grundbuchauskunft des Kantons Uri. Nur Netz plus
/// Parser, keine Regel.
///
/// Die Adresse kommt aus dem Parzellendienst (Feld url_grundbuch) und wird
/// nicht selbst zusammengebaut: aendert der Kanton sie, folgt der Leser von
/// selbst. Ohne Adresse wird nichts geraten.
/// </summary>
public sealed class UriLandRegistryClient : ILandRegistryLookup
{
    private readonly GeoUrHttpGateway _gateway;

    public UriLandRegistryClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<LandRegistryEntry?> ReadAsync(
        ParcelInfo parcel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parcel);

        if (string.IsNullOrWhiteSpace(parcel.LandRegistryUrl))
            return null;

        if (!Uri.TryCreate(parcel.LandRegistryUrl, UriKind.Absolute, out var adresse))
            return null;

        var html = await _gateway.GetStringAsync(adresse, ct).ConfigureAwait(false);
        return LandRegistryHtmlParser.Parse(html);
    }
}
