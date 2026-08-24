using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Telefon und Mail der Eigentuemer aus dem Verzeichnis.
///
/// Die Bedingungen von search.ch untersagen maschinelle Massenabfragen
/// ausdruecklich. Das Kontingent je Liegenschaft und die Regel „nur ein
/// eindeutiger Treffer" sind deshalb keine Bequemlichkeit, sondern eine
/// Grenze — und gehoeren geprueft.
/// </summary>
public sealed class OwnerDirectoryLookupUseCaseTests
{
    private sealed class FakeDirectory : IDirectoryLookup
    {
        private readonly Func<string, DirectoryLookupResult> _antwort;

        public FakeDirectory(Func<string, DirectoryLookupResult> antwort, bool eingerichtet = true)
        {
            _antwort = antwort;
            IsConfigured = eingerichtet;
        }

        public List<string> Gefragt { get; } = new();

        public bool IsConfigured { get; }

        public string Attribution => "Quelle: Test";

        public Task<DirectoryLookupResult> FindAsync(
            string name, string town, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Gefragt.Add(name);
            return Task.FromResult(_antwort(name));
        }
    }

    private static DirectoryLookupResult Eindeutig(string telefon, string mail)
        => new(new[] { new DirectoryEntry("Wer auch immer", "", "", "", telefon, mail) });

    private static DossierDefinition MitEigentuemern(params string[] namen)
    {
        var dossier = new DossierDefinition { Town = "Erstfeld" };
        foreach (var name in namen)
            dossier.Owners.Add(new DossierOwnerRow { Name = name });
        return dossier;
    }

    [Fact]
    public async Task Ein_eindeutiger_Treffer_wird_uebernommen()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041 000 00 00", "a@b.ch"));
        var dossier = MitEigentuemern("Dittli Hans");

        var uebernommen = await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(1, uebernommen);
        Assert.Equal("041 000 00 00", dossier.Owners[0].Phone);
        Assert.Equal("a@b.ch", dossier.Owners[0].Mail);
    }

    [Fact]
    public async Task Bei_mehreren_Treffern_wird_nichts_uebernommen()
    {
        // Eine geratene Nummer im Brief ist schlimmer als eine leere Zelle.
        var dienst = new FakeDirectory(_ => new DirectoryLookupResult(new[]
        {
            new DirectoryEntry("A", "", "", "", "041 111 11 11", ""),
            new DirectoryEntry("B", "", "", "", "041 222 22 22", "")
        }));

        var dossier = MitEigentuemern("Müller");

        var uebernommen = await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(0, uebernommen);
        Assert.Equal("", dossier.Owners[0].Phone);
    }

    [Fact]
    public async Task Hoechstens_fuenf_Abfragen_je_Liegenschaft()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041", "a@b.ch"));
        var dossier = MitEigentuemern("A", "B", "C", "D", "E", "F", "G");

        await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(OwnerDirectoryLookupUseCase.MaxQueriesPerProperty, dienst.Gefragt.Count);
        Assert.Equal(5, dienst.Gefragt.Count);
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, dienst.Gefragt);
    }

    [Fact]
    public async Task Eine_Zeile_ohne_Namen_verbraucht_kein_Kontingent()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041", "a@b.ch"));
        var dossier = MitEigentuemern("", "   ", "A", "B");

        await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(new[] { "A", "B" }, dienst.Gefragt);
    }

    [Fact]
    public async Task Ohne_eingerichteten_Dienst_wird_gar_nicht_gefragt()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041", "a@b.ch"), eingerichtet: false);
        var dossier = MitEigentuemern("A");

        var uebernommen = await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(0, uebernommen);
        Assert.Empty(dienst.Gefragt);
    }

    [Fact]
    public async Task Ein_Fehler_bei_einer_Person_stoppt_die_uebrigen_nicht()
    {
        var dienst = new FakeDirectory(name => name == "A"
            ? throw new InvalidOperationException("Dienst weg")
            : Eindeutig("041", "a@b.ch"));

        var dossier = MitEigentuemern("A", "B");

        var uebernommen = await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal(1, uebernommen);
        Assert.Equal("041", dossier.Owners[1].Phone);
    }

    [Fact]
    public async Task Ein_bereits_erfasster_Wert_wird_nicht_ueberschrieben()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041 999 99 99", "neu@b.ch"));
        var dossier = MitEigentuemern("A");
        dossier.Owners[0].Phone = "von Hand";

        await new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier);

        Assert.Equal("von Hand", dossier.Owners[0].Phone);
        Assert.Equal("neu@b.ch", dossier.Owners[0].Mail);
    }

    [Fact]
    public async Task Ein_Abbruch_bricht_wirklich_ab()
    {
        var dienst = new FakeDirectory(_ => Eindeutig("041", "a@b.ch"));
        var dossier = MitEigentuemern("A", "B");

        using var abbruch = new CancellationTokenSource();
        await abbruch.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new OwnerDirectoryLookupUseCase(dienst).FillAsync(dossier, abbruch.Token));

        Assert.Empty(dienst.Gefragt);
    }
}
