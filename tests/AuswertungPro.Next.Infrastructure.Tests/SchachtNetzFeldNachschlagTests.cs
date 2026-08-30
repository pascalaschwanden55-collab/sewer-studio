using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Im Feld "Eigentuemer" eines Schachts steht der Eigentuemer des BAUWERKS —
/// Privat, Abwasser Uri, Kanton Uri, eine Gemeinde. Das ist etwas anderes als
/// der Grundstueckseigentuemer im Eigentuemerdossier: Bei manchen Leitungen
/// gehoert die Anlage nicht dem, dem das Land gehoert.
///
/// Der Wert steht im Layer leitungen:abw_normschaechte. Die XTF fuehrt ihn
/// nicht — dort tragen alle Bauwerke denselben Verweis.
/// </summary>
public sealed class SchachtNetzFeldNachschlagTests
{
    private sealed class FesteSchaechte : ISchachtNetzLookup
    {
        private readonly IReadOnlyList<NetworkSchacht> _treffer;
        public IReadOnlyList<string>? LetzteNamen { get; private set; }

        public FesteSchaechte(params NetworkSchacht[] treffer) => _treffer = treffer;

        public Task<IReadOnlyList<NetworkSchacht>> FindByNamesAsync(
            IReadOnlyList<string> namen, CancellationToken ct = default)
        {
            LetzteNamen = namen;
            return Task.FromResult(_treffer);
        }
    }

    private sealed class DrosselndeSchaechte : ISchachtNetzLookup
    {
        public Task<IReadOnlyList<NetworkSchacht>> FindByNamesAsync(
            IReadOnlyList<string> namen, CancellationToken ct = default)
            => throw new GeoUrRequestFailedException("Der Kartendienst antwortete mit 429.");
    }

    private static NetworkSchacht Schacht(string nummer, string eigentuemer)
        => new(nummer, eigentuemer)
        {
            Funktion = "Einlaufschacht",
            Material = "unbekannt",
            Nutzungsart = "Regenabwasser",
            Status = "in_Betrieb",
        };

    [Fact]
    public async Task Der_Eigentuemer_des_Bauwerks_kommt_aus_dem_Netzdienst()
    {
        var dienst = new SchachtNetzFeldNachschlag(
            new FesteSchaechte(Schacht("33434", "Privat")));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("33434", "Eigentuemer"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Privat", vorschlag.Wert);
        Assert.Contains("Abwassernetz", vorschlag.QuelleKlartext, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Abwasser Uri")]
    [InlineData("Kanton Uri")]
    [InlineData("Erstfeld")]
    public async Task Auch_die_uebrigen_Eigentuemer_kommen_unveraendert_an(string eigentuemer)
    {
        var dienst = new SchachtNetzFeldNachschlag(
            new FesteSchaechte(Schacht("33434", eigentuemer)));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("33434", "Eigentuemer"));

        Assert.Equal(
            eigentuemer,
            Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag.Wert);
    }

    [Fact]
    public async Task Genau_der_gefragte_Schacht_wird_abgefragt()
    {
        var netz = new FesteSchaechte(Schacht("33434", "Privat"));
        var dienst = new SchachtNetzFeldNachschlag(netz);

        await dienst.SucheAsync(new FeldNachschlagAnfrage("33434", "Eigentuemer"));

        Assert.NotNull(netz.LetzteNamen);
        Assert.Single(netz.LetzteNamen!);
        Assert.Equal("33434", netz.LetzteNamen![0]);
    }

    [Fact]
    public async Task Ein_unbekannter_Schacht_meldet_nicht_gefunden()
    {
        var dienst = new SchachtNetzFeldNachschlag(new FesteSchaechte());

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("99999", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Mehrere_Treffer_derselben_Nummer_werden_nicht_geraten()
    {
        var dienst = new SchachtNetzFeldNachschlag(new FesteSchaechte(
            Schacht("33434", "Privat"), Schacht("33434", "Erstfeld")));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("33434", "Eigentuemer"));

        Assert.Equal(2, Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis).Kandidaten.Count);
    }

    [Fact]
    public async Task Eine_Drosselung_ist_ein_eigener_Zustand()
    {
        var dienst = new SchachtNetzFeldNachschlag(new DrosselndeSchaechte());

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("33434", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.Gedrosselt>(ergebnis);
    }

    [Fact]
    public void Der_Schacht_Eigentuemer_kommt_aus_dem_Abwassernetz_nicht_aus_dem_Grundbuch()
    {
        // Im Dossier geht es um den Grundstuecksbesitzer; hier um den
        // Eigentuemer des Bauwerks. Beides ist nicht dasselbe.
        Assert.Equal(
            FeldQuelle.Abwassernetz,
            FeldQuellenTabelle.QuelleFuer("Eigentuemer", BauteilArt.Schacht));

        // Die Gebaeudeadresse bleibt beim Grundbuch - die kennt nur es.
        Assert.Equal(
            FeldQuelle.Grundbuch,
            FeldQuellenTabelle.QuelleFuer("Strasse", BauteilArt.Schacht));
    }
}
