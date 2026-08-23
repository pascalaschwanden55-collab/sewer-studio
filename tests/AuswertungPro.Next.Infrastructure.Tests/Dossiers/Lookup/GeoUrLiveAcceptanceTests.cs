using System;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Der einzige Test, der mit den echten Diensten spricht. Er beweist, dass die
/// Leser die tatsaechliche Antwort verstehen — eine Fixture kann das nicht,
/// weil sie den Aufbau nur nachbaut.
///
/// Geprueft wird die Parzelle 439 in Erstfeld (BFS 1206) mit den am 2026-08-23
/// gemessenen Werten. Aendert der Kanton den Aufbau seiner Seiten, wird dieser
/// Test rot — genau dort, wo es auffallen muss.
///
/// Es werden bewusst KEINE Personennamen geprueft: die Zusicherungen kommen
/// ohne sie aus.
/// </summary>
public sealed class GeoUrLiveAcceptanceTests
{
    [Fact]
    public async Task Die_echten_Dienste_liefern_die_gemessenen_Werte_fuer_Parzelle_439()
    {
        using var gateway = new GeoUrHttpGateway();

        var parzellen = new UriParcelWfsClient(gateway);
        var parzelle = await parzellen.FindAsync(1206, "439");

        Assert.NotNull(parzelle);
        Assert.Equal("439", parzelle!.Number);
        Assert.Equal(1206, parzelle.BfsNr);
        Assert.Equal(1139, parzelle.AreaSqm);
        Assert.Equal("CH114627077847", parzelle.Egrid);
        Assert.StartsWith("POLYGON((", parzelle.OutlineWkt, StringComparison.Ordinal);
        Assert.Contains("grundbuchauskunft", parzelle.LandRegistryUrl, StringComparison.OrdinalIgnoreCase);

        // Zwei Miteigentuemer mit Kennzeichnung — ohne die Namen zu pruefen.
        var grundbuch = new UriLandRegistryClient(gateway);
        var eintrag = await grundbuch.ReadAsync(parzelle);

        Assert.NotNull(eintrag);
        Assert.False(eintrag!.NoOwnerRegistered);
        Assert.Equal(2, eintrag.Owners.Count);
        Assert.All(eintrag.Owners, o => Assert.False(string.IsNullOrWhiteSpace(o.Name)));
        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("Lit.B", eintrag.Owners[1].Designation);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Erstfeld", eintrag.Town);

        // Namensfreie NEGATIV-Pruefung: findet Zeichensalat, falls doch
        // einmal ein Umlaut auftaucht. Sie ist bei umlautfreien Namen (wie
        // hier bei Parzelle 439) leer wahr und belegt die Kodierung deshalb
        // NICHT — den positiven Nachweis liefert der eigene Test
        // Die_Auskunft_wird_wirklich_als_ISO_8859_1_gelesen weiter unten.
        Assert.All(eintrag.Owners, o =>
        {
            Assert.DoesNotContain('�', o.Name);
            Assert.DoesNotContain("Ã", o.Name, StringComparison.Ordinal);
            Assert.DoesNotContain('�', o.AddressLine);
            Assert.DoesNotContain("Ã", o.AddressLine, StringComparison.Ordinal);
        });

        // Sechs Haltungen auf der Parzelle, davon fuenf privat.
        var netz = new UriSewerNetworkWfsClient(gateway);
        var haltungen = await netz.FindOnParcelAsync(parzelle);

        Assert.Equal(6, haltungen.Count);
        Assert.Equal(5, haltungen.Count(h => h.IsPrivate));
        Assert.Contains(haltungen, h => h.Designation == "36051-36329");

        // Und die Sammelabfrage findet dieselbe Haltung ueber ihren Namen.
        var nachName = await netz.FindByNamesAsync(new[] { "36051-36329" });
        Assert.Single(nachName);
    }

    [Fact]
    public async Task Die_Gemeindeliste_enthaelt_die_19_Urner_Gemeinden()
    {
        using var gateway = new GeoUrHttpGateway();

        var gemeinden = await new UriParcelWfsClient(gateway).ListMunicipalitiesAsync();

        Assert.Equal(19, gemeinden.Count);
        Assert.Contains(gemeinden, g => g.BfsNr == 1206 && g.Name == "Erstfeld");
    }

    [Fact]
    public async Task Die_Auskunft_wird_wirklich_als_ISO_8859_1_gelesen()
    {
        // Positiver Nachweis statt blosser Abwesenheit von Zeichensalat: die
        // Seite traegt das Wort "Eigentümer" als rohes Latin-1-Byte. Wird sie
        // als UTF-8 gelesen, steht dort ein Ersatzzeichen — und ein Umlaut in
        // einem echten Namen waere genauso verstuemmelt.
        //
        // Das Wort ist eine feste Beschriftung der Seite, kein Personenname.
        using var gateway = new GeoUrHttpGateway();

        var parzelle = await new UriParcelWfsClient(gateway).FindAsync(1206, "439");
        Assert.NotNull(parzelle);
        Assert.False(string.IsNullOrWhiteSpace(parzelle!.LandRegistryUrl));

        var seite = await gateway.GetStringAsync(new Uri(parzelle.LandRegistryUrl));

        Assert.NotNull(seite);
        Assert.Contains("Eigentümer", seite!, StringComparison.Ordinal);
        Assert.DoesNotContain('�', seite);
    }
}
