using System.Globalization;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ChfAccountingDisplayConverterTests
{
    private readonly ChfAccountingDisplayConverter _converter = new();

    [Fact]
    public void Mehrdeutiger_Punktwert_wird_nicht_als_1_25_Franken_gespeichert()
    {
        var result = _converter.ConvertBack(
            "1.250", typeof(string), "amount", CultureInfo.GetCultureInfo("de-CH"));

        Assert.Equal("1.250", result);
    }

    [Theory]
    [InlineData("1'250", "1250.00")]
    [InlineData("1 250", "1250.00")]
    [InlineData("1.25", "1.25")]
    [InlineData("1,25", "1.25")]
    [InlineData("(CHF 1'250)", "-1250.00")]
    public void Eindeutige_Geldeingaben_werden_kulturunabhaengig_gespeichert(
        string input,
        string expected)
    {
        var result = _converter.ConvertBack(
            input, typeof(string), "amount", CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(expected, result);
    }
}
