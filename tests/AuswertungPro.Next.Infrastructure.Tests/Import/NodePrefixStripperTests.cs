using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer NodePrefixStripper.
/// Sichert das IST-Verhalten aus IbakExportImportService.StripNodePrefixes.
/// </summary>
public class NodePrefixStripperTests
{
    // --- NodePrefixRegex ---

    [Theory]
    [InlineData("07.", true)]
    [InlineData("10.", true)]
    [InlineData("1.", true)]
    [InlineData("07.1028055", true)]  // matches prefix only (regex ^)
    [InlineData("1028055", false)]
    [InlineData("abc", false)]
    public void NodePrefixRegex_PraefixErkannt(string input, bool erwartet)
        => Assert.Equal(erwartet, NodePrefixStripper.NodePrefixRegex.IsMatch(input));

    // --- StripNodePrefixes ---

    [Fact]
    public void Strip_BeideTeileHabenPraefix_BeideEntfernt()
        => Assert.Equal("1028055-1064892",
            NodePrefixStripper.StripNodePrefixes("07.1028055-10.1064892"));

    [Fact]
    public void Strip_EinTeilHatPraefix_NurDerEntfernt()
        => Assert.Equal("1028055-1064892",
            NodePrefixStripper.StripNodePrefixes("07.1028055-1064892"));

    [Fact]
    public void Strip_KeinPraefix_UnveraendertZurueck()
        => Assert.Equal("1028055-1064892",
            NodePrefixStripper.StripNodePrefixes("1028055-1064892"));

    [Fact]
    public void Strip_KeinBindestrich_NurEinTeilStrip()
        => Assert.Equal("1028055",
            NodePrefixStripper.StripNodePrefixes("07.1028055"));

    [Fact]
    public void Strip_KeinBindestrichKeinPraefix_UnveraendertZurueck()
        => Assert.Equal("1028055",
            NodePrefixStripper.StripNodePrefixes("1028055"));

    [Fact]
    public void Strip_ZweistelligerPraefix_EntferntKorrekt()
        => Assert.Equal("1064892-1099001",
            NodePrefixStripper.StripNodePrefixes("10.1064892-06.1099001"));
}
