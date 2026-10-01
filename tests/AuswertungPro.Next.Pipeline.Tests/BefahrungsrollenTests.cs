using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Arbeitspaket 5 des Uebergabeplans vom 2026-09-05, Schritt 1: die reine
/// Zuordnungsregel. Die zehn Pflichtfaelle des Plans sind hier einzeln abgebildet.
///
/// Kernsatz der Fachregel: <b>Nie "zweite Datei = Gegeninspektion".</b> Zwei
/// verschiedene Videos koennen ebenso gut Wiederholung, Reparaturkontrolle oder
/// Teilaufnahmen sein.
/// </summary>
public sealed class BefahrungsrollenTests
{
    [Fact]
    public void Fall1_HauptdateiUndUnterstrichG_ErgebenGetrennteRollen()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\100-200.mpg"),
            new BefahrungsBeleg(@"C:\q\100-200_G.mpg"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "100-200.mpg"));
        Assert.Equal(Befahrungsrolle.Gegenbefahrung, Rolle(rollen, "100-200_G.mpg"));
    }

    [Fact]
    public void Fall2_IbakTildeG_ErgibtGetrennteRollen()
    {
        var rollen = Ordne("06-001",
            new BefahrungsBeleg(@"C:\q\H_06-001.mpg"),
            new BefahrungsBeleg(@"C:\q\H_06-001~G.mpg"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "H_06-001.mpg"));
        Assert.Equal(Befahrungsrolle.Gegenbefahrung, Rolle(rollen, "H_06-001~G.mpg"));
    }

    [Fact]
    public void Fall3_EntgegengesetzteKamerarichtungAllein_BelegtKeinPaar()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\FILM0001.mpg", Kamerarichtung: "in_Fliessrichtung"),
            new BefahrungsBeleg(@"C:\q\FILM0002.mpg", Kamerarichtung: "gegen_Fliessrichtung"));

        Assert.All(rollen, r => Assert.Equal(Befahrungsrolle.Ungeklaert, r.Rolle));
    }

    [Fact]
    public void Fall4_UmgedrehterHaltungsname_BleibtOffen()
    {
        // "100-200" und "200-100" allein sind KEIN Paarbeweis: Es gibt parallele
        // Leitungen und andere Untersuchungen.
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\100-200.mpg"),
            new BefahrungsBeleg(@"C:\q\200-100.mpg"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "100-200.mpg"));
        Assert.Equal(Befahrungsrolle.Ungeklaert, Rolle(rollen, "200-100.mpg"));
    }

    [Fact]
    public void Fall5_ZweiAufnahmenGleicherRichtung_ErfindenKeineGegeninspektion()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\100-200_0001.mpg", Kamerarichtung: "in_Fliessrichtung", Inhaltsschluessel: "h:A"),
            new BefahrungsBeleg(@"C:\q\100-200_0002.mpg", Kamerarichtung: "in_Fliessrichtung", Inhaltsschluessel: "h:B"));

        Assert.DoesNotContain(rollen, r => r.Rolle == Befahrungsrolle.Gegenbefahrung);
        Assert.Equal(2, rollen.Count(r => r.Rolle == Befahrungsrolle.WeitereAufnahme));
    }

    [Fact]
    public void Fall6_GleicheNamenInZweiProjekten_WerdenNichtGekreuzt()
    {
        var rollen = Ordne("100-200", "Andermatt",
            new BefahrungsBeleg(@"C:\Andermatt\100-200.mpg", Projektschluessel: "Andermatt"),
            new BefahrungsBeleg(@"C:\Hospental\100-200_G.mpg", Projektschluessel: "Hospental"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "100-200.mpg"));
        Assert.Equal(Befahrungsrolle.Ungeklaert, Rolle(rollen, "100-200_G.mpg"));
        Assert.Contains(rollen, r => r.Grund.Contains("anderen Projekt", StringComparison.Ordinal));
    }

    [Fact]
    public void Fall7_ZweiGegenkandidaten_LassenDieGegenseiteOffen()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\100-200.mpg", Inhaltsschluessel: "h:A"),
            new BefahrungsBeleg(@"C:\q\100-200_G.mpg", Inhaltsschluessel: "h:B"),
            new BefahrungsBeleg(@"C:\q\100-200-g.mpg", Inhaltsschluessel: "h:C"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "100-200.mpg"));
        Assert.DoesNotContain(rollen, r => r.Rolle == Befahrungsrolle.Gegenbefahrung);
        Assert.Equal(2, rollen.Count(r => r.Rolle == Befahrungsrolle.Ungeklaert));
    }

    [Fact]
    public void Fall8_IdentischeKopie_IstKeineZweiteBefahrung()
    {
        var rollen = Ordne("327015-2414",
            new BefahrungsBeleg(@"C:\q\Video\Sec\327015-2414.mpg", Inhaltsschluessel: "h:GLEICH"),
            new BefahrungsBeleg(@"C:\q\Misc\Exchange\327015-2414.mpg", Inhaltsschluessel: "h:GLEICH"));

        Assert.Equal(2, rollen.Count);
        Assert.All(rollen, r => Assert.Equal(Befahrungsrolle.Hauptbefahrung, r.Rolle));
        Assert.DoesNotContain(rollen, r => r.Rolle == Befahrungsrolle.Gegenbefahrung);
        Assert.Contains(rollen, r => r.Grund.Contains("bytegleiche Kopie", StringComparison.Ordinal));
    }

    [Fact]
    public void Fall9_GegenmarkeInFliessrichtung_IstKeinWiderspruch()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\100-200_G.mpg", Kamerarichtung: "in_Fliessrichtung"));

        var zuordnung = Assert.Single(rollen);
        Assert.Equal(Befahrungsrolle.Gegenbefahrung, zuordnung.Rolle);
        Assert.DoesNotContain("Widerspruch", zuordnung.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void Fall10_DateireihenfolgeAendertDieRollenNicht()
    {
        var a = new BefahrungsBeleg(@"C:\q\100-200.mpg");
        var b = new BefahrungsBeleg(@"C:\q\100-200_G.mpg");

        var vorwaerts = Ordne("100-200", a, b);
        var rueckwaerts = Ordne("100-200", b, a);

        Assert.Equal(
            vorwaerts.Select(r => (r.Pfad, r.Rolle)),
            rueckwaerts.Select(r => (r.Pfad, r.Rolle)));
    }

    // ---------------------------------------------------------------------
    // Zusatzfaelle
    // ---------------------------------------------------------------------

    [Fact]
    public void GleicherTagAllein_IstKeinPaarbeweis()
    {
        // Der Aufnahmetag steckt in beiden Namen; er belegt keine Gegenrichtung.
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\20200702_100-200.mpg"),
            new BefahrungsBeleg(@"C:\q\20200702_300-400.mpg"));

        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "20200702_100-200.mpg"));
        Assert.Equal(Befahrungsrolle.Ungeklaert, Rolle(rollen, "20200702_300-400.mpg"));
    }

    [Fact]
    public void EinzelnesVideoOhneJedenBeleg_BleibtOffen()
    {
        var rollen = Ordne("100-200", new BefahrungsBeleg(@"C:\q\FILM0007.mpg"));

        Assert.Equal(Befahrungsrolle.Ungeklaert, Assert.Single(rollen).Rolle);
    }

    [Fact]
    public void GegenrichtungMitBeliebigemNamen_BelegtKeineGegenbefahrung()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg(@"C:\q\FILM0009.mpg", Kamerarichtung: "gegen_Fliessrichtung"));

        Assert.Equal(Befahrungsrolle.Ungeklaert, Assert.Single(rollen).Rolle);
    }

    [Fact]
    public void HauptbefahrungDarfUpstreamLaufen()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg("100-200.mpg", "gegen_Fliessrichtung"),
            new BefahrungsBeleg("100-200_G.mpg", "in_Fliessrichtung"));
        Assert.Equal(Befahrungsrolle.Hauptbefahrung, Rolle(rollen, "100-200.mpg"));
        Assert.Equal(Befahrungsrolle.Gegenbefahrung, Rolle(rollen, "100-200_G.mpg"));
    }

    [Fact]
    public void BenanntesPaarMitGleicherRichtung_BleibtOffen()
    {
        var rollen = Ordne("100-200",
            new BefahrungsBeleg("100-200.mpg", "gegen_Fliessrichtung"),
            new BefahrungsBeleg("100-200_G.mpg", "gegen_Fliessrichtung"));
        Assert.DoesNotContain(rollen, r => r.Rolle == Befahrungsrolle.Gegenbefahrung);
        Assert.Contains(rollen, r => r.Grund.Contains("Widerspruch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1100-200.mpg")]
    [InlineData("100-2000.mpg")]
    public void AehnlicheHaltungsnamen_SindKeineZuordnung(string pfad)
        => Assert.Equal(Befahrungsrolle.Ungeklaert, Assert.Single(Ordne("100-200", new BefahrungsBeleg(pfad))).Rolle);

    // ---------------------------------------------------------------------

    private static IReadOnlyList<Befahrungszuordnung> Ordne(string haltung, params BefahrungsBeleg[] belege)
        => Befahrungsrollen.Ordne(haltung, belege);

    private static IReadOnlyList<Befahrungszuordnung> Ordne(
        string haltung, string projekt, params BefahrungsBeleg[] belege)
        => Befahrungsrollen.Ordne(haltung, belege, projekt);

    private static Befahrungsrolle Rolle(IReadOnlyList<Befahrungszuordnung> rollen, string dateiname)
        => rollen.Single(r => r.Pfad.EndsWith(dateiname, StringComparison.OrdinalIgnoreCase)).Rolle;
}
