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
}
