using System.Globalization;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPlanWidthInputParserTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("de-CH")]
    [InlineData("en-US")]
    public void Punkt_und_Komma_liefern_auf_jeder_Windows_Kultur_dieselbe_Breite(
        string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            Assert.Equal(12.5, DossierPlanWidthInputParser.Parse("12.5").WidthCm);
            Assert.Equal(12.5, DossierPlanWidthInputParser.Parse("12,5").WidthCm);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Leere_Breite_verwendet_die_Vorlage_und_ungueltige_Werte_werden_abgelehnt()
    {
        var empty = DossierPlanWidthInputParser.Parse(" ");
        var tooWide = DossierPlanWidthInputParser.Parse("15.1");
        var invalid = DossierPlanWidthInputParser.Parse("abc");

        Assert.True(empty.Success);
        Assert.Null(empty.WidthCm);
        Assert.False(tooWide.Success);
        Assert.False(invalid.Success);
    }
}
