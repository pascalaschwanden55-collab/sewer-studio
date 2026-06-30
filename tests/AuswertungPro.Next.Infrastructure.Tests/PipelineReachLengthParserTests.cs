using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PipelineReachLengthParserTests
{
    [Theory]
    [InlineData("12.5", 12.5d)]
    [InlineData("12,5", 12.5d)]
    [InlineData(" 7,25 ", 7.25d)]
    [InlineData("1,234", 1.234d)]
    public void TryParse_parst_positive_laengen_mit_punkt_oder_komma(string raw, double expected)
        => Assert.Equal(expected, PipelineReachLengthParser.TryParse(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParse_liefert_null_bei_leeren_ungueltigen_oder_nicht_positiven_werten(string? raw)
        => Assert.Null(PipelineReachLengthParser.TryParse(raw));
}
