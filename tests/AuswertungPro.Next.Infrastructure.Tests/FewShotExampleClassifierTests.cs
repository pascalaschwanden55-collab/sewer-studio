// Charakterisierungstests für FewShotExampleClassifier (pure static Helfer)
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FewShotExampleClassifierTests
{
    // ---- DetermineQuality -------------------------------------------------

    [Theory]
    [InlineData("BAA",  0.9)]  // Hochwertiger Strukturschaden
    [InlineData("BAAXA",0.9)]  // Langer Code mit HighValue-Prefix
    [InlineData("BAB",  0.9)]
    [InlineData("BAC",  0.9)]
    [InlineData("BAF",  0.9)]
    [InlineData("BBA",  0.9)]
    [InlineData("BBC",  0.9)]
    [InlineData("BBD",  0.9)]
    [InlineData("BDA",  0.3)]  // Allgemeinzustand → niedrige Qualitaet
    [InlineData("BDAXY",0.3)]  // Langer Code mit BDA-Prefix
    [InlineData("BCA",  0.7)]  // BC* → mittlere Qualitaet
    [InlineData("BCC",  0.7)]
    [InlineData("A0X",  0.6)]  // A0-Code → 0.6
    [InlineData("B0X",  0.6)]  // B0-Code → 0.6
    [InlineData("XYZ",  0.5)]  // Unbekannt → Default
    public void DetermineQuality_ReturnsExpected(string vsaCode, double expected)
    {
        var entry = MakeEntry(vsaCode);
        var result = FewShotExampleClassifier.DetermineQuality(entry);
        Assert.Equal(expected, result, precision: 10);
    }

    // ---- ExtractClockPosition ---------------------------------------------

    [Theory]
    [InlineData("Riss von 3 Uhr bis 9 Uhr", "3 Uhr bis 9 Uhr")]
    [InlineData("Schaden bei 12 Uhr",        "12 Uhr")]
    [InlineData("von 6 Uhr",                  "6 Uhr")]
    [InlineData("Kein Zeitbezug",             null)]
    [InlineData("",                           null)]
    public void ExtractClockPosition_ReturnsExpected(string text, string? expected)
    {
        var result = FewShotExampleClassifier.ExtractClockPosition(text);
        Assert.Equal(expected, result);
    }

    // ---- Hilfsmethode -------------------------------------------------

    private static GroundTruthEntry MakeEntry(string vsaCode) =>
        new()
        {
            MeterStart = 0,
            MeterEnd   = 0,
            VsaCode    = vsaCode,
            Text       = string.Empty,
        };
}
