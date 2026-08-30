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
/// Der Grundbuchweg darf niemals raten. Eine falsche Parzelle bedeutet einen
/// falschen Eigentuemer — und damit im schlimmsten Fall einen Brief an einen
/// Unbeteiligten.
/// </summary>
public sealed class GrundbuchFeldNachschlagTests
{
    private sealed class FesteParzellen : IParcelLookup
    {
        private readonly IReadOnlyList<ParcelInfo> _treffer;
        public IReadOnlyList<string>? LetzteLinien { get; private set; }

        public FesteParzellen(params ParcelInfo[] treffer) => _treffer = treffer;

        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => Task.FromResult<ParcelInfo?>(null);

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
        {
            LetzteLinien = wktLines;
            return Task.FromResult(_treffer);
        }

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>([]);
    }

    private sealed class DrosselndeParzellen : IParcelLookup
    {
        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => throw new GeoUrRequestFailedException("Der Kartendienst antwortete mit 429.");

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FestesGrundbuch : ILandRegistryLookup
    {
        private readonly LandRegistryEntry? _eintrag;
        public FestesGrundbuch(LandRegistryEntry? eintrag) => _eintrag = eintrag;

        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_eintrag);
    }

    private static ParcelInfo Parzelle(string nummer)
        => new(nummer, 1210, "Erstfeld", 500, "CH1", "POLYGON((0 0))", "https://example.invalid");

    private static LandRegistryEntry Eintrag(params string[] eigentuemer)
    {
        var owners = new List<LandRegistryOwner>();
        foreach (var name in eigentuemer)
            owners.Add(new LandRegistryOwner("Lit.A", name, "Musterweg 4", "1/1"));

        return new LandRegistryEntry("Musterweg", "4", "6472", "Erstfeld", owners, false);
    }

    [Fact]
    public void Aus_einem_Punkt_wird_eine_kurze_Linie()
    {
        var wkt = PunktAlsKurzeLinie.Baue(2692606.892, 1192380.717);

        // Eine Linie von einem Meter Laenge, mittig auf dem Punkt.
        Assert.Contains("2692606.392", wkt, StringComparison.Ordinal);
        Assert.Contains("2692607.392", wkt, StringComparison.Ordinal);
        Assert.Contains("1192380.717", wkt, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Punktlinie_wird_wirklich_an_den_Dienst_gegeben()
    {
        var parzellen = new FesteParzellen(Parzelle("439"));
        var dienst = new GrundbuchFeldNachschlag(
            _ => (2692606.892, 1192380.717), parzellen, new FestesGrundbuch(Eintrag("Muster, Hans")));

        _ = dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer")).Result;

        Assert.NotNull(parzellen.LetzteLinien);
        Assert.Single(parzellen.LetzteLinien!);
        Assert.Contains("2692606.392", parzellen.LetzteLinien![0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Eigentuemer_wird_direkt_vorgeschlagen()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new FesteParzellen(Parzelle("439")),
            new FestesGrundbuch(Eintrag("Muster, Hans")));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Muster, Hans", vorschlag.Wert);
        Assert.Contains("Parzelle 439", vorschlag.QuelleKlartext, StringComparison.Ordinal);
        Assert.Contains("Erstfeld", vorschlag.QuelleKlartext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mehrere_Eigentuemer_werden_zur_Auswahl_gestellt()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new FesteParzellen(Parzelle("439")),
            new FestesGrundbuch(Eintrag("Muster, Hans", "Beispiel, Anna")));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Mehrere_Parzellen_werden_nicht_geraten()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new FesteParzellen(Parzelle("439"), Parzelle("440")),
            new FestesGrundbuch(Eintrag("Muster, Hans")));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Die_Strasse_kommt_mit_Hausnummer()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new FesteParzellen(Parzelle("439")),
            new FestesGrundbuch(Eintrag("Muster, Hans")));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Strasse"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Musterweg 4", vorschlag.Wert);
    }

    [Fact]
    public async Task Ohne_Lage_gibt_es_gar_keine_Abfrage()
    {
        var parzellen = new FesteParzellen(Parzelle("439"));
        var dienst = new GrundbuchFeldNachschlag(
            _ => null, parzellen, new FestesGrundbuch(Eintrag("Muster, Hans")));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("99999", "Eigentuemer"));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Kataster", nicht.Grund, StringComparison.OrdinalIgnoreCase);
        // Kein Netzzugriff ohne Lage - das zaehlt gegen die Drosselung.
        Assert.Null(parzellen.LetzteLinien);
    }

    [Fact]
    public async Task Ohne_eingetragenen_Eigentuemer_wird_nichts_erfunden()
    {
        var ohne = new LandRegistryEntry("Musterweg", "4", "6472", "Erstfeld", [], true);
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new FesteParzellen(Parzelle("439")), new FestesGrundbuch(ohne));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Eine_Drosselung_ist_ein_eigener_Zustand()
    {
        var dienst = new GrundbuchFeldNachschlag(
            _ => (1.0, 2.0), new DrosselndeParzellen(), new FestesGrundbuch(null));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        // Nicht "nicht gefunden": Der Bearbeiter soll wissen, dass es an der
        // Drosselung liegt und ein spaeterer Versuch hilft.
        Assert.IsType<FeldNachschlagErgebnis.Gedrosselt>(ergebnis);
    }
}
