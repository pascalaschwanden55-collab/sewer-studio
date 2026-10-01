using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest Schaechte aus dem Abwassernetz des Kantons Uri. Gegenstueck zu
/// <see cref="UriSewerNetworkWfsClient"/>, nur fuer die Bauwerke.
///
/// Noetig, weil die XTF den Eigentuemer nicht fuehrt: Dort tragen alle
/// Bauwerke denselben Verweis, obwohl der Kanton Privat, Gemeinden, ASTRA und
/// weitere unterscheidet.
/// </summary>
public sealed class UriSchachtWfsClient : ISchachtNetzLookup
{
    private const string Dienst = "https://geo.ur.ch/wfs";
    private const string Ebene = "leitungen:abw_normschaechte";

    /// <summary>Mehr Namen je Anfrage machen die Adresszeile zu lang.</summary>
    private const int NamenJeAnfrage = 25;

    private readonly GeoUrHttpGateway _gateway;

    public UriSchachtWfsClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<IReadOnlyList<NetworkSchacht>> FindByNamesAsync(
        IReadOnlyList<string> namen, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(namen);

        var sauber = namen
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ergebnis = new List<NetworkSchacht>();

        for (var i = 0; i < sauber.Count; i += NamenJeAnfrage)
        {
            ct.ThrowIfCancellationRequested();

            var teil = sauber.Skip(i).Take(NamenJeAnfrage)
                .Select(n => "'" + n.Replace("'", "''") + "'");
            var filter = "bw_bezeichnung IN (" + string.Join(",", teil) + ")";

            var xml = await _gateway.GetStringAsync(BaueAbfrage(filter), ct).ConfigureAwait(false);
            ergebnis.AddRange(SchachtWfsXmlParser.Parse(xml));
        }

        return ergebnis;
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
