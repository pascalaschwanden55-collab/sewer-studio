using AuswertungPro.Next.Application.CodeCatalog;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 Phase 3.2: VsaCodeTree.GetQuantificationUnit muss Einheiten aus QuantRules
/// korrekt ableiten. Basis fuer XTF/PDF/Qwen-Import.
/// </summary>
public class GetQuantificationUnitTests
{
    [Theory]
    [InlineData("BAA", 1, "%")]   // Verformung
    [InlineData("BAG", 1, "%")]   // Einragung
    [InlineData("BAB", 1, null)]  // Char1-abhaengig (nur BABB / BABC haben Einheit)
    [InlineData("BABB", 1, "mm")] // Rissbreite bei B
    [InlineData("BABC", 1, "mm")] // Rissbreite bei C
    [InlineData("BAC", 1, "mm")]  // Bruchlaenge
    [InlineData("BAE", 1, "mm")]  // Tiefe Moertel
    [InlineData("BCD", 1, null)]  // Rohranfang — keine Q
    [InlineData("BCE", 1, null)]  // Rohrende — keine Q
    [InlineData("BDA", 1, null)]  // Allg. Foto — keine Q
    [InlineData("UNKNOWN", 1, null)]
    [InlineData("", 1, null)]
    [InlineData("BAA", 3, null)]  // Ungueltiger Index
    public void ReturnsExpectedUnit(string code, int index, string? expected)
    {
        var actual = VsaCodeTree.GetQuantificationUnit(code, index);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WorksForPointNotation()
    {
        // BAB.B → nach Normalisierung BABB → Rissbreite mm
        Assert.Equal("mm", VsaCodeTree.GetQuantificationUnit("BAB.B", 1));
        Assert.Equal("mm", VsaCodeTree.GetQuantificationUnit("bab.b", 1));
    }
}
