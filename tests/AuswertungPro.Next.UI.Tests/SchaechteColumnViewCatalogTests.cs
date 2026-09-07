using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteColumnViewCatalogTests
{
    [Fact]
    public void Fuenf_Ansichten_mit_der_Schachtnummer_in_jeder()
    {
        Assert.Equal(new[] { "kompakt", "zustand", "sanierung", "medien", "alle" }, SchaechteColumnViewCatalog.Views.Select(v => v.Key).ToArray());
        foreach (var v in SchaechteColumnViewCatalog.Views.Where(v => v.Felder is not null))
            Assert.Contains("Schachtnummer", v.Felder!);
        Assert.Equal(9, SchaechteColumnViewCatalog.Resolve("kompakt").Felder!.Count);
    }

    [Fact]
    public void Unbekannter_Schluessel_faellt_auf_Alle_zurueck()
        => Assert.Equal("alle", SchaechteColumnViewCatalog.Resolve("gibtsnicht").Key);
}
