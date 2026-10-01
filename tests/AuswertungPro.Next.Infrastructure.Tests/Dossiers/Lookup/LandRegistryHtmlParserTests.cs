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
    public void Keine_wird_auch_mitten_im_Block_nicht_zum_Namen()
    {
        var html = """
            <html><body><table>
            <tr><td>Grundbuch Musterdorf</td></tr>
            <tr><td>Eigent&#252;mer</td></tr>
            <tr><td>Lit.A:</td></tr>
            <tr><td>Martin Muster</td></tr>
            <tr><td>Musterweg 3, 6472 Musterdorf</td></tr>
            <tr><td>1/2 Miteigentum</td></tr>
            <tr><td>Lit.B:</td></tr>
            <tr><td>Keine</td></tr>
            <tr><td>Anmerkungen(nur &#246;ffentlich einsehbare)</td></tr>
            </table></body></html>
            """;

        var eintrag = LandRegistryHtmlParser.Parse(html);

        Assert.NotNull(eintrag);
        Assert.DoesNotContain(eintrag!.Owners,
            o => o.Name.Equals("Keine", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void Liest_alle_Stockwerkeigentuemer_aus_der_einzeiligen_Form()
    {
        // Beim Stockwerkeigentum steht der ganze Eintrag in EINER Zeile:
        //   "Lit.: Jeweiliger Eigentuemer von StWE S1021 (Name), 31/100 Miteigentum"
        // Der Name steht in der Klammer, nicht am Zeilenanfang.
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_stockwerkeigentum.html"));

        Assert.NotNull(eintrag);
        Assert.Equal(3, eintrag!.Owners.Count);

        Assert.Equal("Kurt Beispiel, Rita Beispiel", eintrag.Owners[0].Name);
        Assert.Equal("StWE S1021", eintrag.Owners[0].Designation);
        Assert.Equal("31/100 Miteigentum", eintrag.Owners[0].Share);

        Assert.Equal("Martin Muster und Anna Muster Eheleute", eintrag.Owners[1].Name);
        Assert.Equal("StWE S1022", eintrag.Owners[1].Designation);

        Assert.Equal("Peter Beispiel-Muster und Sara Claire Eheleute", eintrag.Owners[2].Name);
        Assert.Equal("37/100 Miteigentum", eintrag.Owners[2].Share);

        // Die Liegenschaftsadresse steht in der Gebaeudezeile, nicht beim Eigentuemer.
        Assert.Equal("Musterweg", eintrag.BuildingStreet);
        Assert.Equal("51", eintrag.BuildingHouseNumber);
    }

    [Fact]
    public void Kein_Eigentuemername_traegt_noch_Verwaltungstext()
    {
        // Ein Name, der "Lit." oder "StWE" enthaelt, ist nie ein Personenname —
        // er wuerde so in den Brief an den Eigentuemer gedruckt.
        var eintrag = LandRegistryHtmlParser.Parse(Lade("grundbuch_stockwerkeigentum.html"));

        Assert.NotNull(eintrag);
        Assert.All(eintrag!.Owners, o =>
        {
            Assert.False(o.Name.Contains("Lit.", StringComparison.OrdinalIgnoreCase),
                "Der Eigentuemername enthaelt noch die Kennzeichnung.");
            Assert.False(o.Name.Contains("StWE", StringComparison.OrdinalIgnoreCase),
                "Der Eigentuemername enthaelt noch den Stockwerkeigentums-Verweis.");
            Assert.False(o.Name.Contains("/", StringComparison.Ordinal),
                "Der Eigentuemername enthaelt noch den Anteil.");
        });
    }
}
