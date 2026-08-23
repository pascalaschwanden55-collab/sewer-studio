using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest Liegenschaften aus dem WFS des Kantons Uri. Nur Netz plus Parser,
/// keine Regel.
/// </summary>
public sealed class UriParcelWfsClient : IParcelLookup
{
    private const string Dienst = "https://geo.ur.ch/wfs";
    private const string EbeneParzellen = "av:ch059_liegenschaften_flaechen";
    private const string EbeneGemeinden = "av:ch062_hoheitsgrenzen_gemeindegrenzen";

    private readonly GeoUrHttpGateway _gateway;

    public UriParcelWfsClient(GeoUrHttpGateway gateway)
        => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<ParcelInfo?> FindAsync(
        int bfsNr, string parcelNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parcelNumber))
            return null;

        // Ueber die BFS-Nummer suchen, nicht ueber den Gemeindenamen: Schreibweisen
        // wie "Altdorf (UR)" sind damit kein Thema.
        var filter = $"nummer='{Maskiere(parcelNumber)}' AND bfsnr={bfsNr.ToString(CultureInfo.InvariantCulture)}";
        var xml = await _gateway.GetStringAsync(BaueAbfrage(EbeneParzellen, filter), ct)
            .ConfigureAwait(false);

        var parzellen = ParcelWfsXmlParser.Parse(xml);
        return parzellen.Count == 1 ? parzellen[0] : null;
    }

    public async Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
        IReadOnlyList<string> wktLines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(wktLines);
        if (wktLines.Count == 0)
            return Array.Empty<ParcelInfo>();

        var linien = string.Join(",", ExtrahiereLinienkoerper(wktLines));
        var filter = $"INTERSECTS(wkb_geometry,MULTILINESTRING({linien}))";

        // Per POST, weil der Filter fuer ein ganzes Projekt mehrere tausend
        // Zeichen lang wird und nicht in eine Adresszeile gehoert.
        var xml = await _gateway.PostFormAsync(
            new Uri(Dienst),
            new Dictionary<string, string>
            {
                ["service"] = "WFS",
                ["version"] = "2.0.0",
                ["request"] = "GetFeature",
                ["typeNames"] = EbeneParzellen,
                ["srsName"] = "EPSG:2056",
                ["CQL_FILTER"] = filter
            },
            ct).ConfigureAwait(false);

        return ParcelWfsXmlParser.Parse(xml);
    }

    public async Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(
        CancellationToken ct = default)
    {
        var xml = await _gateway
            .GetStringAsync(BaueAbfrage(EbeneGemeinden, filter: null), ct)
            .ConfigureAwait(false);

        return ParcelWfsXmlParser.ParseMunicipalities(xml);
    }

    private static Uri BaueAbfrage(string ebene, string? filter)
    {
        var abfrage = HttpUtility.ParseQueryString(string.Empty);
        abfrage["service"] = "WFS";
        abfrage["version"] = "2.0.0";
        abfrage["request"] = "GetFeature";
        abfrage["typeNames"] = ebene;
        abfrage["srsName"] = "EPSG:2056";
        if (!string.IsNullOrWhiteSpace(filter))
            abfrage["CQL_FILTER"] = filter;

        return new Uri(Dienst + "?" + abfrage);
    }

    /// <summary>
    /// Bringt jede Linie auf die Teilform "(a b,c d)", damit sie in eine
    /// gemeinsame MULTILINESTRING-Geometrie passt.
    ///
    /// Eine Haltung kann mehrteilig sein: dann liefert der Leser
    /// "MULTILINESTRING((...),(...))" und die inneren Teile werden einzeln
    /// uebernommen. Ein blosses Abschneiden vor der ersten Klammer ergaebe eine
    /// verschachtelte und damit ungueltige Geometrie.
    /// </summary>
    private static IEnumerable<string> ExtrahiereLinienkoerper(IReadOnlyList<string> wktLines)
    {
        foreach (var linie in wktLines)
        {
            if (string.IsNullOrWhiteSpace(linie))
                continue;

            var text = linie.Trim();
            if (!text.EndsWith(")", StringComparison.Ordinal))
                continue;

            if (text.StartsWith("MULTILINESTRING", StringComparison.OrdinalIgnoreCase))
            {
                var innen = text[(text.IndexOf('(') + 1)..^1].Trim();
                foreach (var teil in TeileEinerMehrfachlinie(innen))
                    yield return teil;

                continue;
            }

            if (text.StartsWith("LINESTRING", StringComparison.OrdinalIgnoreCase))
                yield return text[text.IndexOf('(')..];
        }
    }

    /// <summary>
    /// Zerlegt "(a b,c d),(e f,g h)" in die einzelnen Teile. Getrennt wird nur
    /// auf oberster Klammerebene — ein Komma innerhalb eines Teils trennt nichts.
    /// </summary>
    private static IEnumerable<string> TeileEinerMehrfachlinie(string innen)
    {
        var tiefe = 0;
        var start = -1;

        for (var i = 0; i < innen.Length; i++)
        {
            if (innen[i] == '(')
            {
                if (tiefe == 0)
                    start = i;

                tiefe++;
            }
            else if (innen[i] == ')')
            {
                tiefe--;
                if (tiefe == 0 && start >= 0)
                {
                    yield return innen[start..(i + 1)];
                    start = -1;
                }
            }
        }
    }

    private static string Maskiere(string wert) => wert.Replace("'", "''");
}
