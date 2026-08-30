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
/// Der Eigentuemer einer Haltung kommt aus dem Abwassernetz-Dienst des
/// Kantons, nicht aus der XTF: Der Export dorthin plattet die Zuordnung ein
/// und wuerde fuer jede Leitung "Abwasser Uri" behaupten.
///
/// Am 2026-08-30 gegen den echten Dienst geprueft: Die Haltungen 36262-36275,
/// 33458-36051 und 36275-35558 liefern dort "Privat" — in der XTF stehen
/// dieselben Leitungen mit dem Verweis auf Abwasser Uri.
/// </summary>
public sealed class NetzFeldNachschlagTests
{
    private sealed class FestesNetz : ISewerNetworkLookup
    {
        private readonly IReadOnlyList<NetworkHolding> _treffer;
        public IReadOnlyList<string>? LetzteNamen { get; private set; }

        public FestesNetz(params NetworkHolding[] treffer) => _treffer = treffer;

        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> names, CancellationToken ct = default)
        {
            LetzteNamen = names;
            return Task.FromResult(_treffer);
        }

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>([]);
    }

    private sealed class DrosselndesNetz : ISewerNetworkLookup
    {
        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> names, CancellationToken ct = default)
            => throw new GeoUrRequestFailedException("Der Kartendienst antwortete mit 429.");

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static NetworkHolding Haltung(string name, string eigentuemer, double? laenge = 3.61)
        => new(name, eigentuemer, laenge, "LINESTRING(0 0, 1 1)");

    [Fact]
    public async Task Der_Eigentuemer_kommt_aus_dem_Netzdienst()
    {
        var netz = new FestesNetz(Haltung("36262-36275", "Privat"));
        var dienst = new NetzFeldNachschlag(netz);

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer", BauteilArt.Haltung));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Privat", vorschlag.Wert);
        Assert.Contains("Abwassernetz", vorschlag.QuelleKlartext, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Genau_die_gefragte_Haltung_wird_abgefragt()
    {
        var netz = new FestesNetz(Haltung("36262-36275", "Privat"));
        var dienst = new NetzFeldNachschlag(netz);

        await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer", BauteilArt.Haltung));

        // Kein Sammellauf: eine Anfrage, ein Name.
        Assert.NotNull(netz.LetzteNamen);
        Assert.Single(netz.LetzteNamen!);
        Assert.Equal("36262-36275", netz.LetzteNamen![0]);
    }

    [Fact]
    public async Task Eine_unbekannte_Haltung_meldet_nicht_gefunden()
    {
        var dienst = new NetzFeldNachschlag(new FestesNetz());

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("439.01-36051", "Eigentuemer", BauteilArt.Haltung));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Abwassernetz", nicht.Grund, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ein_leerer_Eigentuemer_wird_nicht_als_Wert_ausgegeben()
    {
        var dienst = new NetzFeldNachschlag(new FestesNetz(Haltung("36262-36275", "  ")));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer", BauteilArt.Haltung));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Mehrere_Treffer_desselben_Namens_werden_nicht_geraten()
    {
        var dienst = new NetzFeldNachschlag(new FestesNetz(
            Haltung("36262-36275", "Privat"),
            Haltung("36262-36275", "Erstfeld")));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer", BauteilArt.Haltung));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Eine_Drosselung_ist_ein_eigener_Zustand()
    {
        var dienst = new NetzFeldNachschlag(new DrosselndesNetz());

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer", BauteilArt.Haltung));

        Assert.IsType<FeldNachschlagErgebnis.Gedrosselt>(ergebnis);
    }
}
