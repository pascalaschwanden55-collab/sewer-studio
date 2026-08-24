using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class DossierParcelLookupUseCaseTests
{
    private static ParcelInfo Parzelle(string nummer = "30")
        => new(nummer, 1208, "Musterdorf", 552, "CH1", "POLYGON((0 0,1 0,1 1,0 0))", "url");

    private static LandRegistryEntry Auszug(params string[] namen)
        => new(
            "Musterweg", "51", "6472", "Musterdorf",
            namen.Select(n => new LandRegistryOwner("", n, "", "")).ToList(),
            NoOwnerRegistered: false);

    private sealed class Parzellen : IParcelLookup
    {
        public ParcelInfo? Treffer { get; init; }
        public Exception? Fehler { get; init; }

        public Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct = default)
            => Fehler is not null ? Task.FromException<ParcelInfo?>(Fehler) : Task.FromResult(Treffer);

        public Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(
            IReadOnlyList<string> wktLines, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ParcelInfo>>(Array.Empty<ParcelInfo>());

        public Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Municipality>>(Array.Empty<Municipality>());
    }

    private sealed class Grundbuch : ILandRegistryLookup
    {
        public LandRegistryEntry? Treffer { get; init; }
        public Exception? Fehler { get; init; }

        public Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct = default)
            => Fehler is not null
                ? Task.FromException<LandRegistryEntry?>(Fehler)
                : Task.FromResult(Treffer);
    }

    private sealed class Netz : ISewerNetworkLookup
    {
        public IReadOnlyList<NetworkHolding> Treffer { get; init; } = Array.Empty<NetworkHolding>();
        public Exception? Fehler { get; init; }

        public Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(
            ParcelInfo parcel, CancellationToken ct = default)
            => Fehler is not null
                ? Task.FromException<IReadOnlyList<NetworkHolding>>(Fehler)
                : Task.FromResult(Treffer);

        public Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(
            IReadOnlyList<string> designations, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NetworkHolding>>(Array.Empty<NetworkHolding>());
    }

    [Fact]
    public async Task Fuellt_Adresse_Ort_und_alle_Eigentuemer_vor()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle() },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel", "Rita Beispiel") },
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "30");

        Assert.True(ergebnis.Found);
        Assert.Equal("30", ergebnis.Dossier!.ParcelNumbers);
        Assert.Equal("Musterdorf", ergebnis.Dossier.Municipality);
        Assert.Equal(1208, ergebnis.Dossier.MunicipalityBfsNr);
        Assert.Equal("Musterweg", ergebnis.Dossier.Address);
        Assert.Equal("51", ergebnis.Dossier.HouseNumbers);
        Assert.Equal("6472", ergebnis.Dossier.PostalCode);
        Assert.Equal(2, ergebnis.Dossier.Owners.Count);
        Assert.Empty(ergebnis.Warnings);
    }

    [Fact]
    public async Task Telefon_und_Mail_bleiben_leer_denn_sie_stehen_nicht_im_Grundbuch()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle() },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel") },
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "30");

        Assert.All(ergebnis.Dossier!.Owners, o =>
        {
            Assert.Equal("", o.Phone);
            Assert.Equal("", o.Mail);
        });
    }

    [Fact]
    public async Task Eine_unbekannte_Parzelle_wird_klar_gemeldet()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = null }, new Grundbuch(), new Netz());

        var ergebnis = await fall.RunAsync(1208, "999999");

        Assert.False(ergebnis.Found);
        Assert.Contains(ergebnis.Warnings, w => w.Contains("999999", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Dienstfehler_ist_ein_Fehler_und_kein_leeres_Ergebnis()
    {
        // Dieselbe Falle wie in der Stapelanlage: ein stiller Fehler erzeugt
        // ein Dossier ohne die Haelfte seiner Angaben.
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Fehler = new InvalidOperationException("Dienst weg") },
            new Grundbuch(),
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "30");

        Assert.False(ergebnis.Found);
        Assert.Contains(ergebnis.Warnings, w => w.Contains("Dienst weg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ohne_Grundbuchauszug_bleibt_wenigstens_die_Parzelle()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle("77") },
            new Grundbuch { Fehler = new InvalidOperationException("Seite kaputt") },
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "77");

        Assert.True(ergebnis.Found);
        Assert.Equal("77", ergebnis.Dossier!.ParcelNumbers);
        Assert.Empty(ergebnis.Dossier.Owners);
        Assert.Contains(ergebnis.Warnings, w => w.Contains("Seite kaputt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Eine_Parzelle_ohne_eingetragenen_Eigentuemer_wird_gemeldet()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle() },
            new Grundbuch
            {
                Treffer = new LandRegistryEntry(
                    "Musterweg", "51", "6472", "Musterdorf",
                    Array.Empty<LandRegistryOwner>(),
                    NoOwnerRegistered: true)
            },
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "30");

        Assert.True(ergebnis.Found);
        Assert.Contains(ergebnis.Warnings, w => w.Contains("kein Eigentümer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Fehler_bei_den_Leitungen_verhindert_das_Dossier_nicht()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle() },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel") },
            new Netz { Fehler = new InvalidOperationException("Netz weg") });

        var ergebnis = await fall.RunAsync(1208, "30");

        Assert.True(ergebnis.Found);
        Assert.Empty(ergebnis.Holdings);
        Assert.Contains(ergebnis.Warnings, w => w.Contains("Netz weg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Die_privaten_Leitungen_des_Projekts_kommen_ueber_ihren_Namen_dazu()
    {
        // Der Kanton fuehrt sie nicht — ihr Knotenname zeigt aber auf die
        // Parzelle. Ohne diesen Weg fehlten genau die Hausanschluesse.
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle("439") },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel") },
            new Netz());

        var ergebnis = await fall.RunAsync(
            1208, "439", new[] { "439.01-36051", "12345-12346" });

        var leitung = Assert.Single(ergebnis.Holdings);
        Assert.Equal("439.01-36051", leitung.Designation);
        Assert.Equal("Name", leitung.Origin);
        Assert.True(leitung.InProject);
        Assert.True(leitung.Preselected);
    }

    [Fact]
    public async Task Eine_Leitung_die_das_Projekt_nicht_fuehrt_ist_nicht_vorgewaehlt()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle("439") },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel") },
            new Netz
            {
                Treffer = new[]
                {
                    new NetworkHolding("77000-77001", "Privat", 12.5, "LINESTRING(0 0,1 1)")
                }
            });

        var ergebnis = await fall.RunAsync(1208, "439", Array.Empty<string>());

        var leitung = Assert.Single(ergebnis.Holdings);
        Assert.Equal("Lage", leitung.Origin);
        Assert.False(leitung.InProject);
        Assert.False(leitung.Preselected);
    }

    [Fact]
    public async Task Dieselbe_Leitung_aus_beiden_Wegen_erscheint_nur_einmal()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle("439") },
            new Grundbuch { Treffer = Auszug("Kurt Beispiel") },
            new Netz
            {
                Treffer = new[]
                {
                    new NetworkHolding("439.01-36051", "Privat", 12.5, "LINESTRING(0 0,1 1)")
                }
            });

        var ergebnis = await fall.RunAsync(1208, "439", new[] { "439.01-36051" });

        var leitung = Assert.Single(ergebnis.Holdings);
        Assert.Equal("Lage", leitung.Origin);
        Assert.True(leitung.InProject);
    }

    [Fact]
    public async Task Auch_ohne_Grundbuchauszug_werden_die_Leitungen_zugeordnet()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Treffer = Parzelle("439") },
            new Grundbuch { Fehler = new InvalidOperationException("Seite kaputt") },
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "439", new[] { "439.01-36051" });

        Assert.True(ergebnis.Found);
        Assert.Single(ergebnis.Holdings);
    }

    [Fact]
    public async Task Eine_leere_Parzellennummer_fragt_gar_nichts_ab()
    {
        var fall = new DossierParcelLookupUseCase(
            new Parzellen { Fehler = new InvalidOperationException("darf nicht aufgerufen werden") },
            new Grundbuch(),
            new Netz());

        var ergebnis = await fall.RunAsync(1208, "   ");

        Assert.False(ergebnis.Found);
        Assert.Single(ergebnis.Warnings);
    }
}
