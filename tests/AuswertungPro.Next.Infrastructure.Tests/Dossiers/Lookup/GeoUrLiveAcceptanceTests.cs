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
///
/// Diese drei Tests laufen nur, wenn die Umgebungsvariable
/// <c>SEWER_GEOUR_LIVE_ACCEPTANCE=1</c> gesetzt ist — siehe
/// <see cref="GeoUrLiveFactAttribute"/>. Ohne sie werden sie uebersprungen, damit
/// der oeffentliche Dienst des Kantons nicht bei jedem Testlauf angefragt wird und
/// nicht drosselt.
/// </summary>
public sealed class GeoUrLiveAcceptanceTests
{
    [GeoUrLiveFact]
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

        // Bewusst KEIN Assert.All und KEIN Assert.Contains/DoesNotContain auf
        // dem Namen selbst: xUnit haengt einem Fehlschlag sowohl den ToString()
        // des Elements (ein Record — das waere der ganze Eintrag samt echtem
        // Namen) als auch den geprueften Text an die Meldung an. Beides wuerde
        // einen echten Eigentuemernamen ins Testprotokoll schreiben.
        foreach (var eigentuemer in eintrag.Owners)
        {
            Assert.True(!string.IsNullOrWhiteSpace(eigentuemer.Name),
                "Ein Eigentuemername ist leer.");
        }

        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("Lit.B", eintrag.Owners[1].Designation);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Erstfeld", eintrag.Town);

        // Namensfreie NEGATIV-Pruefung: findet Zeichensalat, falls doch
        // einmal ein Umlaut auftaucht. Sie ist bei umlautfreien Namen (wie
        // hier bei Parzelle 439) leer wahr und belegt die Kodierung deshalb
        // NICHT — den positiven Nachweis liefert der eigene Test
        // Die_Auskunft_wird_wirklich_als_ISO_8859_1_gelesen weiter unten.
        foreach (var eigentuemer in eintrag.Owners)
        {
            var nameOhneZeichensalat =
                !eigentuemer.Name.Contains('�')
                && !eigentuemer.Name.Contains("Ã", StringComparison.Ordinal);
            var adresseOhneZeichensalat =
                !eigentuemer.AddressLine.Contains('�')
                && !eigentuemer.AddressLine.Contains("Ã", StringComparison.Ordinal);

            Assert.True(nameOhneZeichensalat,
                "Ein Eigentuemername enthält Zeichensalat — die Kodierung stimmt nicht.");
            Assert.True(adresseOhneZeichensalat,
                "Eine Adresse enthält Zeichensalat — die Kodierung stimmt nicht.");
        }

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

    [GeoUrLiveFact]
    public async Task Die_Gemeindeliste_enthaelt_die_19_Urner_Gemeinden()
    {
        using var gateway = new GeoUrHttpGateway();

        var gemeinden = await new UriParcelWfsClient(gateway).ListMunicipalitiesAsync();

        Assert.Equal(19, gemeinden.Count);
        Assert.Contains(gemeinden, g => g.BfsNr == 1206 && g.Name == "Erstfeld");
    }

    [GeoUrLiveFact]
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

        var traegtUmlaut = seite!.Contains("Eigentümer", StringComparison.Ordinal);
        var traegtErsatzzeichen = seite.Contains('�');

        // Bewusst Assert.True mit eigener Meldung: Assert.Contains wuerde im
        // Fehlerfall die ganze Seite ausgeben — samt echter Eigentuemernamen.
        Assert.True(traegtUmlaut,
            "Die Beschriftung 'Eigentümer' wurde nicht mit Umlaut gelesen — die Kodierung stimmt nicht.");
        Assert.False(traegtErsatzzeichen,
            "Die gelesene Seite enthält Ersatzzeichen — die Kodierung stimmt nicht.");
    }
}
