using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class LandRegistryHtmlParserTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public void Liest_einen_einzelnen_Eigentuemer_mit_Adresse()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_einzeleigentuemer.html"));

        Assert.NotNull(eintrag);
        Assert.False(eintrag!.NoOwnerRegistered);
        Assert.Equal("Musterweg", eintrag.BuildingStreet);
        Assert.Equal("3", eintrag.BuildingHouseNumber);
        Assert.Equal("6472", eintrag.PostalCode);
        Assert.Equal("Musterdorf", eintrag.Town);

        var eigentuemer = Assert.Single(eintrag.Owners);
        Assert.Equal("Martin Muster", eigentuemer.Name);
        Assert.Equal("Musterweg 3, 6472 Musterdorf", eigentuemer.AddressLine);
        Assert.Equal("", eigentuemer.Designation);
    }

    [Fact]
    public void Liest_beide_Miteigentuemer_mit_ihrer_Kennzeichnung()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_miteigentum.html"));

        Assert.NotNull(eintrag);
        Assert.Equal(2, eintrag!.Owners.Count);

        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("Kurt Beispiel", eintrag.Owners[0].Name);
        Assert.Equal("1/2 Miteigentum", eintrag.Owners[0].Share);

        Assert.Equal("Lit.B", eintrag.Owners[1].Designation);
        Assert.Equal("Rita Beispiel", eintrag.Owners[1].Name);

        Assert.Equal("Musterstrasse", eintrag.BuildingStreet);
        Assert.Equal("30", eintrag.BuildingHouseNumber);
    }

    [Fact]
    public void Keine_wird_nie_zu_einem_Namen()
    {
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_ohne_eigentuemer.html"));

        Assert.NotNull(eintrag);
        Assert.True(eintrag!.NoOwnerRegistered);
        Assert.Empty(eintrag.Owners);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html><body>Seite nicht gefunden</body></html>")]
    public void Was_nicht_sicher_gelesen_werden_kann_ergibt_null(string? html)
    {
        // Lieber nichts als ein geratener Name.
        Assert.Null(LandRegistryHtmlParser.Parse(html));
    }

    [Fact]
    public void Keine_bleibt_auch_mit_zusaetzlicher_Zeile_kein_Name()
    {
        var html = """
            <html><body><table>
            <tr><td>Grundbuch Musterdorf</td></tr>
            <tr><td>Liegenschaft Nr. 13</td></tr>
            <tr><td>Eigent&#252;mer</td></tr>
            <tr><td>Keine</td></tr>
            <tr><td>siehe Hinweis unten</td></tr>
            <tr><td>Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
            </table></body></html>
            """;

        var eintrag = LandRegistryHtmlParser.Parse(html);

        Assert.NotNull(eintrag);
        Assert.True(eintrag!.NoOwnerRegistered);
        Assert.DoesNotContain(eintrag.Owners, o => o.Name.Contains("Keine", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ohne_Abschluss_Anmerkungen_wird_nichts_gelesen()
    {
        // Der Seitenrest darf nicht zum Eigentuemerblock werden.
        var html = """
            <html><body><table>
            <tr><td>Grundbuch Musterdorf</td></tr>
            <tr><td>Eigent&#252;mer</td></tr>
            <tr><td>Martin Muster</td></tr>
            <tr><td>Musterweg 3, 6472 Musterdorf</td></tr>
            <tr><td>Dieser Auszug kann nicht als g&#252;ltiger Grundbuchauszug verwendet werden.</td></tr>
            </table></body></html>
            """;

        Assert.Null(LandRegistryHtmlParser.Parse(html));
    }

    [Fact]
    public void Eine_dritte_Zeile_erbt_nicht_die_Kennzeichnung_des_Vorgaengers()
    {
        var html = """
            <html><body><table>
            <tr><td>Grundbuch Musterdorf</td></tr>
            <tr><td>Eigent&#252;mer</td></tr>
            <tr><td>Lit.A:</td></tr>
            <tr><td>Kurt Beispiel</td></tr>
            <tr><td>Musterstrasse 30, 6472 Musterdorf</td></tr>
            <tr><td>1/2 Miteigentum</td></tr>
            <tr><td>Martin Muster</td></tr>
            <tr><td>Musterweg 3, 6472 Musterdorf</td></tr>
            <tr><td>Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
            </table></body></html>
            """;

        var eintrag = LandRegistryHtmlParser.Parse(html);

        Assert.NotNull(eintrag);
        Assert.Equal(2, eintrag!.Owners.Count);
        Assert.Equal("Lit.A", eintrag.Owners[0].Designation);
        Assert.Equal("", eintrag.Owners[1].Designation);
    }
}
