using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest das Abwassernetz des Kantons Uri. Nur Netz plus Parser, keine Regel.
/// </summary>
public sealed class UriSewerNetworkWfsClient : ISewerNetworkLookup
{
    private const string Dienst = "https://geo.ur.ch/wfs";
    private const string Ebene = "leitungen:abw_haltungen";

    /// <summary>Mehr Namen je Anfrage machen die Adresszeile zu lang.</summary>
    private const int NamenJeAnfrage = 25;

    private readonly GeoUrHttpGateway _gateway;

    public UriSewerNetworkWfsClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
        IReadOnlyList<string> names, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        var sauber = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ergebnis = new List<NetworkHolding>();

        for (var i = 0; i < sauber.Count; i += NamenJeAnfrage)
        {
            ct.ThrowIfCancellationRequested();

            var teil = sauber.Skip(i).Take(NamenJeAnfrage)
                .Select(n => "'" + n.Replace("'", "''") + "'");
            var filter = "ne_bezeichnung IN (" + string.Join(",", teil) + ")";

            var xml = await _gateway.GetStringAsync(BaueAbfrage(filter), ct).ConfigureAwait(false);
            ergebnis.AddRange(SewerNetworkWfsXmlParser.Parse(xml));
        }

        return ergebnis;
    }

    public async Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
        ParcelInfo parcel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parcel);
        if (string.IsNullOrWhiteSpace(parcel.OutlineWkt))
            return Array.Empty<NetworkHolding>();

        var xml = await _gateway.PostFormAsync(
            new Uri(Dienst),
            new Dictionary<string, string>
            {
                ["service"] = "WFS",
                ["version"] = "2.0.0",
                ["request"] = "GetFeature",
                ["typeNames"] = Ebene,
                ["srsName"] = "EPSG:2056",
                ["CQL_FILTER"] = $"INTERSECTS(wkb_geometry,{parcel.OutlineWkt})"
            },
            ct).ConfigureAwait(false);

        return SewerNetworkWfsXmlParser.Parse(xml);
    }

    private static Uri BaueAbfrage(string filter)
    {
        var abfrage = HttpUtility.ParseQueryString(string.Empty);
        abfrage["service"] = "WFS";
        abfrage["version"] = "2.0.0";
        abfrage["request"] = "GetFeature";
        abfrage["typeNames"] = Ebene;
        abfrage["srsName"] = "EPSG:2056";
        abfrage["CQL_FILTER"] = filter;

        return new Uri(Dienst + "?" + abfrage);
    }
}
