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
}
