using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingQuantificationSeverityPolicyTests
{
    [Theory]
    [InlineData(31, null, null, 5)]
    [InlineData(16, null, null, 4)]
    [InlineData(6, null, null, 3)]
    [InlineData(null, 21, null, 4)]
    [InlineData(null, 11, null, 3)]
    [InlineData(null, null, 51, 3)]
    [InlineData(null, null, 21, 2)]
    [InlineData(null, null, null, 2)]
    public void Estimate_applies_existing_quantification_priority(
        int? crossSectionReduction,
        int? intrusion,
        int? height,
        int expected)
    {
        var quantification = new MaskQuantificationService.QuantifiedMask(
            "label",
            0.9,
            height,
            WidthMm: null,
            ExtentPercent: null,
            crossSectionReduction,
            intrusion,
            ClockPosition: null);

        Assert.Equal(expected, CodingQuantificationSeverityPolicy.Estimate(quantification));
    }
}
