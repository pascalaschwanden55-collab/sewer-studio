using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierBatchProposalUseCaseTests
{
    // Die gemessene Lage auf Parzelle 439: sechs Haltungen, davon eine dem
    // Kanton und eine private, die das Projekt nicht kennt.
    private static readonly NetworkHolding[] AufParzelle439 =
    {
        new("33429-7.26990", "Privat", 10.96, "LINESTRING(1 1,2 2)"),
        new("36329-35558", "Abwasser Uri", 18.24, "LINESTRING(1 1,2 2)"),
        new("36275-35558", "Privat", 15.63, "LINESTRING(1 1,2 2)"),
        new("36051-36329", "Privat", 11.46, "LINESTRING(1 1,2 2)"),
        new("36052-36329", "Privat", 12.9, "LINESTRING(1 1,2 2)"),
        new("33458-36051", "Privat", 3.32, "LINESTRING(1 1,2 2)")
    };

    private static readonly string[] ImProjekt =
    {
        "36329-35558", "36275-35558", "36051-36329", "36052-36329", "33458-36051"
    };

    private static ParcelInfo Parzelle(string nummer)
        => new(nummer, 1206, "Musterdorf", 1139, "CH1", "POLYGON((0 0,1 0,1 1,0 0))",
            "https://example.invalid/gb");

    private static LandRegistryEntry Miteigentum()
        => new("Musterstrasse", "30", "6472", "Musterdorf", new[]
        {
            new LandRegistryOwner("Lit.A", "Kurt Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum"),
            new LandRegistryOwner("Lit.B", "Rita Beispiel", "Musterstrasse 30, 6472 Musterdorf", "1/2 Miteigentum")
        }, NoOwnerRegistered: false);

    [Fact]
    public async Task Waehlt_genau_die_vier_privaten_Leitungen_aus_dem_Projekt_vor()
    {
        var use = Baue(
            parzellen: new[] { Parzelle("439") },
            aufParzelle: AufParzelle439,
            registry: Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            progress: null, ct: CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.Equal(4, vorschlag.Holdings.Count(h => h.Preselected));

        Assert.All(
            vorschlag.Holdings.Where(h => h.Preselected),
            h => Assert.True(h.IsPrivate && h.InProject));

        // Die Leitung des Kantons und die projektfremde erscheinen, aber unangehakt.
        Assert.Contains(vorschlag.Holdings, h => h.Designation == "36329-35558" && !h.Preselected);
        Assert.Contains(vorschlag.Holdings, h => h.Designation == "33429-7.26990" && !h.Preselected);
    }

    [Fact]
    public async Task Beide_Miteigentuemer_erscheinen_und_der_Name_kommt_vom_ersten()
    {
        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.Equal(2, vorschlag.Registry!.Owners.Count);
        Assert.Equal("Liegenschaft Nr. 439 Beispiel", vorschlag.SuggestedName);
        Assert.True(vorschlag.Selectable);
    }

    [Fact]
    public async Task Ohne_eingetragenen_Eigentuemer_ist_der_Vorschlag_nicht_waehlbar()
    {
        var ohne = new LandRegistryEntry("", "", "", "Musterdorf",
            Array.Empty<LandRegistryOwner>(), NoOwnerRegistered: true);

        var use = Baue(new[] { Parzelle("13") }, Array.Empty<NetworkHolding>(), ohne);

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
        Assert.Contains("kein Eigent", vorschlag.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eine_Parzelle_mit_bestehendem_Dossier_wird_nicht_erneut_angeboten()
    {
        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, new[] { "439" }),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
        Assert.Contains("Dossier", vorschlag.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eine_aus_dem_Namen_abgeleitete_Nummer_ohne_Bestaetigung_wird_verworfen()
    {
        // Der Parzellendienst kennt 439 nicht: dann darf sie nicht erscheinen.
        var use = Baue(
            parzellen: Array.Empty<ParcelInfo>(),
            aufParzelle: Array.Empty<NetworkHolding>(),
            registry: null);

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, new[] { "439.01-36051" }, Array.Empty<string>()),
            null, CancellationToken.None);

        Assert.Empty(ergebnis.Proposals);
    }

    [Fact]
    public async Task Ein_Abbruch_bricht_wirklich_ab()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        var use = Baue(new[] { Parzelle("439") }, AufParzelle439, Miteigentum());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => use.RunAsync(
                new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
                null, quelle.Token));
    }

    [Fact]
    public async Task Ein_Dienstfehler_wird_als_Warnung_gemeldet_und_stoppt_den_Lauf_nicht()
    {
        var use = new DossierBatchProposalUseCase(
            new FakeParcels(new[] { Parzelle("439") }),
            new FehlerhafteRegistry(),
            new FakeNetwork(AufParzelle439));

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, ImProjekt, Array.Empty<string>()),
            null, CancellationToken.None);

        Assert.NotEmpty(ergebnis.Warnings);
        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.False(vorschlag.Selectable);
    }

    [Fact]
    public async Task Eine_dem_Kanton_unbekannte_Leitung_kommt_ueber_ihren_Namen_dazu()
    {
        // Der Kanton kennt auf der Parzelle nur die eine Leitung. Der private
        // Hausanschluss 439.01-36051 steht nur im Projekt — sein Knotenname
        // nennt die Parzelle und bringt ihn hinein.
        var aufParzelle = new[]
        {
            new NetworkHolding("36051-36329", "Privat", 11.46, "LINESTRING(1 1,2 2)")
        };

        var use = Baue(new[] { Parzelle("439") }, aufParzelle, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(
                1206, new[] { "36051-36329", "439.01-36051" }, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        Assert.Equal(2, vorschlag.Holdings.Count);

        var ueberDenNamen = Assert.Single(vorschlag.Holdings, h => h.Origin == "Name");
        Assert.Equal("439.01-36051", ueberDenNamen.Designation);
        Assert.True(ueberDenNamen.IsPrivate);
        Assert.True(ueberDenNamen.InProject);
        Assert.True(ueberDenNamen.Preselected);

        Assert.Equal("36051-36329", Assert.Single(vorschlag.Holdings, h => h.Origin == "Lage").Designation);
    }

    [Fact]
    public async Task Eine_Leitung_die_beide_Wege_finden_erscheint_nur_einmal()
    {
        var aufParzelle = new[]
        {
            new NetworkHolding("439.01-36051", "Privat", 5.0, "LINESTRING(1 1,2 2)")
        };

        var use = Baue(new[] { Parzelle("439") }, aufParzelle, Miteigentum());

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(1206, new[] { "439.01-36051" }, Array.Empty<string>()),
            null, CancellationToken.None);

        var vorschlag = Assert.Single(ergebnis.Proposals);
        var leitung = Assert.Single(vorschlag.Holdings);

        // Die Lage gewinnt: sie kennt den echten Eigentuemer.
        Assert.Equal("Lage", leitung.Origin);
    }

    [Fact]
    public async Task Zwei_Parzellen_bekommen_jede_ihre_eigenen_Leitungen()
    {
        var jeParzelle = new Dictionary<string, NetworkHolding[]>(StringComparer.Ordinal)
        {
            ["439"] = new[] { new NetworkHolding("36051-36329", "Privat", 11.46, "LINESTRING(1 1,2 2)") },
            ["441"] = new[] { new NetworkHolding("36052-36329", "Privat", 12.9, "LINESTRING(3 3,4 4)") }
        };

        var use = new DossierBatchProposalUseCase(
            new FakeParcels(new[] { Parzelle("439"), Parzelle("441") }),
            new FakeRegistry(Miteigentum()),
            new ParzellenweisesNetz(jeParzelle));

        var ergebnis = await use.RunAsync(
            new DossierBatchProposalRequest(
                1206, new[] { "36051-36329", "36052-36329" }, Array.Empty<string>()),
            null, CancellationToken.None);

        Assert.Equal(2, ergebnis.Proposals.Count);

        var vier39 = Assert.Single(ergebnis.Proposals, p => p.Parcel.Number == "439");
        var vier41 = Assert.Single(ergebnis.Proposals, p => p.Parcel.Number == "441");

        Assert.Equal("36051-36329", Assert.Single(vier39.Holdings).Designation);
        Assert.Equal("36052-36329", Assert.Single(vier41.Holdings).Designation);
    }

    /// <summary>Liefert je Parzelle andere Leitungen — anders als der einfache Fake.</summary>
    private sealed class ParzellenweisesNetz : ISewerNetworkLookup
    {
        private readonly IReadOnlyDictionary<string, NetworkHolding[]> _jeParzelle;

        public ParzellenweisesNetz(IReadOnlyDictionary<string, NetworkHolding[]> jeParzelle)
            => _jeParzelle = jeParzelle;

        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> names, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>(
                _jeParzelle.Values.SelectMany(h => h)
                    .Where(h => names.Contains(h.Designation)).ToList());

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>(
                _jeParzelle.TryGetValue(parcel.Number, out var treffer)
                    ? treffer
                    : Array.Empty<NetworkHolding>());
    }

    private static DossierBatchProposalUseCase Baue(
        IReadOnlyList<ParcelInfo> parzellen,
        IReadOnlyList<NetworkHolding> aufParzelle,
        LandRegistryEntry? registry)
        => new(new FakeParcels(parzellen), new FakeRegistry(registry), new FakeNetwork(aufParzelle));

    private sealed class FakeParcels : IParcelLookup
    {
        private readonly IReadOnlyList<ParcelInfo> _parzellen;
        public FakeParcels(IReadOnlyList<ParcelInfo> parzellen) => _parzellen = parzellen;

        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => Task.FromResult(_parzellen.FirstOrDefault(p => p.Number == parcelNumber));

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => Task.FromResult(_parzellen);

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>(new[] { new Municipality(1206, "Musterdorf") });
    }

    private sealed class FakeRegistry : ILandRegistryLookup
    {
        private readonly LandRegistryEntry? _eintrag;
        public FakeRegistry(LandRegistryEntry? eintrag) => _eintrag = eintrag;

        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_eintrag);
    }

    private sealed class FehlerhafteRegistry : ILandRegistryLookup
    {
        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => throw new InvalidOperationException("Dienst nicht erreichbar");
    }

    private sealed class FakeNetwork : ISewerNetworkLookup
    {
        private readonly IReadOnlyList<NetworkHolding> _aufParzelle;
        public FakeNetwork(IReadOnlyList<NetworkHolding> aufParzelle) => _aufParzelle = aufParzelle;

        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> names, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>(
                _aufParzelle.Where(h => names.Contains(h.Designation)).ToList());

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => Task.FromResult(_aufParzelle);
    }
}
