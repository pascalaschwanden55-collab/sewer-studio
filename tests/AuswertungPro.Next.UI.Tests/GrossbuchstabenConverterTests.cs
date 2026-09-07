using System.Globalization;
using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class GrossbuchstabenConverterTests
{
    [Theory]
    [InlineData("strasse", "STRASSE")]
    [InlineData("Kosten", "KOSTEN")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Anwenden_schreibt_gross(string? wert, string? erwartet)
        => Assert.Equal(erwartet, GrossbuchstabenConverter.Anwenden(wert));

    [Fact]
    public void Convert_laesst_nicht_textuelle_Werte_unveraendert()
    {
        var converter = new GrossbuchstabenConverter();
        var objekt = new object();
        Assert.Same(objekt, converter.Convert(objekt, typeof(string), null!, CultureInfo.InvariantCulture));
        Assert.Equal("STRASSE", converter.Convert("strasse", typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_ist_nicht_unterstuetzt()
    {
        var converter = new GrossbuchstabenConverter();
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack("STRASSE", typeof(string), null!, CultureInfo.InvariantCulture));
    }
}
