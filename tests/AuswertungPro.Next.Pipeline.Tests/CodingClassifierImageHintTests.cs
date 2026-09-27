using AuswertungPro.Next.Application.UseCases.CodingClassifierHint;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingClassifierImageHintTests
{
    [Fact]
    public void Bildhinweis_bleibt_ungepruefte_Hauptgruppe_ohne_erfundene_Geometrie()
    {
        var hint = CodingClassifierImageHint.Create("BAI", 0.98, "Einragendes Dichtungsmaterial");
        Assert.NotNull(hint);
        Assert.Equal("BAI", hint.Code);
        Assert.Contains("Einragendes Dichtungsmaterial", hint.Status);
        Assert.Contains("bitte prüfen", hint.Status);
        Assert.Contains("Ungeprüfte Bildklassifikation", hint.Detail);
        Assert.Contains("nicht bestimmt", hint.Detail);
        Assert.StartsWith(hint.Status, hint.BuildStatus("Sehr langer bisheriger Status"));
        Assert.StartsWith("SAM ausgefallen |", hint.AppendToDetail("SAM ausgefallen"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("LEER")]
    [InlineData("OTHER")]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BAIZ")]
    [InlineData("BAQ")]
    public void Struktur_Leer_und_unbekannte_Codes_erzeugen_keinen_zusaetzlichen_Schadenshinweis(string? code)
        => Assert.Null(CodingClassifierImageHint.Create(code, 0.9, "Katalogtext"));

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Fehlender_oder_ungueltiger_Modellwert_wird_nicht_als_sicherer_Hinweis_gezeigt(double? confidence)
        => Assert.Null(CodingClassifierImageHint.Create("BAI", confidence, "Dichtungsmaterial"));
}
