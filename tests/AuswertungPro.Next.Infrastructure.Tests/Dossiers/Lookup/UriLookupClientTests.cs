using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class UriLookupClientTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public async Task Parzellensuche_gibt_die_gelesene_Parzelle_zurueck()
    {
        var handler = new FesteAntwort(Lade("wfs_parzelle.xml"));
        var client = new UriParcelWfsClient(new GeoUrHttpGateway(handler));

        var parzelle = await client.FindAsync(1206, "439");

        Assert.NotNull(parzelle);
        Assert.Equal("439", parzelle!.Number);
        Assert.Contains("nummer", handler.LetzteAnfrage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1206", handler.LetzteAnfrage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Haltungssuche_liest_die_Antwort()
    {
        var handler = new FesteAntwort(Lade("wfs_haltungen.xml"));
        var client = new UriSewerNetworkWfsClient(new GeoUrHttpGateway(handler));

        var haltungen = await client.FindByNamesAsync(new[] { "36051-36329" });

        Assert.Equal(2, haltungen.Count);
    }

    [Fact]
    public async Task Grundbuchauskunft_liest_die_Seite()
    {
        var handler = new FesteAntwort(Lade("grundbuch_miteigentum.html"));
        var client = new UriLandRegistryClient(new GeoUrHttpGateway(handler));

        var parzelle = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1", "POLYGON((0 0,1 0,1 1,0 0))",
            "https://geo.ur.ch/grundbuchauskunft?gem=1206&nr=439");

        var eintrag = await client.ReadAsync(parzelle);

        Assert.NotNull(eintrag);
        Assert.Equal(2, eintrag!.Owners.Count);
    }

    [Fact]
    public async Task Ein_Serverfehler_ergibt_null_statt_einer_Ausnahme()
    {
        var handler = new FesteAntwort("", HttpStatusCode.InternalServerError);
        var client = new UriParcelWfsClient(new GeoUrHttpGateway(handler));

        Assert.Null(await client.FindAsync(1206, "439"));
    }

    [Fact]
    public async Task Ohne_Adresse_der_Grundbuchauskunft_wird_nicht_geraten()
    {
        var handler = new FesteAntwort(Lade("grundbuch_miteigentum.html"));
        var client = new UriLandRegistryClient(new GeoUrHttpGateway(handler));

        var ohneUrl = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "");

        Assert.Null(await client.ReadAsync(ohneUrl));
    }

    [Fact]
    public async Task Unbrauchbare_Linien_fuehren_zu_keiner_Abfrage()
    {
        var handler = new FesteAntwort(Lade("wfs_parzelle.xml"));
        var client = new UriParcelWfsClient(new GeoUrHttpGateway(handler));

        var treffer = await client.FindTouchedAsync(new[] { "", "kein WKT", "POINT(1 1)" });

        Assert.Empty(treffer);
        Assert.Equal(string.Empty, handler.LetzteAnfrage);
    }

    [Fact]
    public async Task Eine_Antwort_in_ISO_8859_1_wird_richtig_gelesen()
    {
        // Echte Latin-1-Bytes, kein ASCII mit Entities: nur so faellt auf, wenn
        // die Kodierung ignoriert wird. Aus "Muesterli" mit Umlaut wuerde sonst
        // ein Fragezeichen — und ein verstuemmelter Name im Brief.
        var html = """
            <html><body><table>
            <tr><td>Grundbuch Musterdorf</td></tr>
            <tr><td>Liegenschaft Nr. 439</td></tr>
            <tr><td>1'139 m2 Gebäude, Musterstrasse 30 (148 m2)</td></tr>
            <tr><td>Eigentümer</td></tr>
            <tr><td>Kurt Müller-Beispiel</td></tr>
            <tr><td>Musterstrasse 30, 6472 Musterdorf</td></tr>
            <tr><td>Anmerkungen</td></tr>
            </table></body></html>
            """;

        var handler = new FesteBytes(
            System.Text.Encoding.Latin1.GetBytes(html), "text/html", "iso-8859-1");
        var client = new UriLandRegistryClient(new GeoUrHttpGateway(handler));

        var parzelle = new ParcelInfo("439", 1206, "Musterdorf", 1139, "CH1",
            "POLYGON((0 0,1 0,1 1,0 0))", "https://example.invalid/gb?gem=1206&nr=439");

        var eintrag = await client.ReadAsync(parzelle);

        Assert.NotNull(eintrag);
        var eigentuemer = Assert.Single(eintrag!.Owners);
        Assert.Equal("Kurt Müller-Beispiel", eigentuemer.Name);
        Assert.DoesNotContain('�', eigentuemer.Name);
    }

    [Fact]
    public async Task Dieselben_Bytes_als_UTF8_gelesen_ergaeben_Zeichensalat()
    {
        // Der Gegenbeweis: ohne Beachtung der Kodierung entstuende Mojibake.
        // Dieser Test haelt fest, dass der Unterschied ueberhaupt sichtbar ist —
        // sonst waere der Test oben wertlos.
        var bytes = System.Text.Encoding.Latin1.GetBytes("Müller");

        Assert.NotEqual("Müller", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal("Müller", System.Text.Encoding.Latin1.GetString(bytes));
    }

    private sealed class FesteAntwort : HttpMessageHandler
    {
        private readonly string _inhalt;
        private readonly HttpStatusCode _status;

        public FesteAntwort(string inhalt, HttpStatusCode status = HttpStatusCode.OK)
        {
            _inhalt = inhalt;
            _status = status;
        }

        public string LetzteAnfrage { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LetzteAnfrage = request.RequestUri?.ToString() ?? string.Empty;
            if (request.Content is not null)
            {
                LetzteAnfrage += " " + await request.Content
                    .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_inhalt)
            };
        }
    }

    /// <summary>Antwortet mit rohen Bytes und einer ausdruecklichen Kodierungsangabe.</summary>
    private sealed class FesteBytes : HttpMessageHandler
    {
        private readonly byte[] _inhalt;
        private readonly string _medientyp;
        private readonly string _kodierung;

        public FesteBytes(byte[] inhalt, string medientyp, string kodierung)
        {
            _inhalt = inhalt;
            _medientyp = medientyp;
            _kodierung = kodierung;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var inhalt = new ByteArrayContent(_inhalt);
            inhalt.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(_medientyp) { CharSet = _kodierung };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = inhalt });
        }
    }
}
