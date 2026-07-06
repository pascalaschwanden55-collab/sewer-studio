using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtSichtbarkeitPolicyTests
{
    [Theory]
    [InlineData(true, 2.388, true)]   // z16 -> Schaechte (Kreise) sichtbar
    [InlineData(true, 4.777, true)]   // z15 -> sichtbar
    [InlineData(true, 5.0, true)]     // Schwelle einschliesslich
    [InlineData(true, 9.55, false)]   // z14 (Startansicht) -> noch aus ("erst beim Reinzoomen")
    [InlineData(false, 1.0, false)]   // ausgeschaltet -> immer aus, egal wie nah
    [InlineData(true, 0.0, false)]    // ungueltige Aufloesung -> aus
    public void ShouldShow_nur_eingeschaltet_und_reingezoomt(bool eingeschaltet, double aufloesung, bool expected)
        => Assert.Equal(expected, SchachtSichtbarkeitPolicy.ShouldShow(eingeschaltet, aufloesung));
}
