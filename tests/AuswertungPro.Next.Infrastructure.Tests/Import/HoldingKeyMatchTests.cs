using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public class HoldingKeyMatchTests
{
    [Theory]
    [InlineData("100-200", "100-200", true)]      // exakt
    [InlineData("100-200", "100-200-1", true)]    // Praefix an Segmentgrenze
    [InlineData("100-200-1", "100-200", true)]    // andere Richtung
    [InlineData("100-200", "100-2000", false)]    // DER Bug: darf NICHT matchen
    [InlineData("100-2000", "100-200", false)]
    [InlineData("100-200", "100-300", false)]
    [InlineData("", "100-200", false)]
    [InlineData("100-200", "", false)]
    public void IsBoundaryPrefixMatch(string a, string b, bool erwartet)
        => Assert.Equal(erwartet, HoldingKeyMatch.IsBoundaryPrefixMatch(a, b));
}
