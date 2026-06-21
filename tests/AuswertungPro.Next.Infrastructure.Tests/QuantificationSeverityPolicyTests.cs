using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class QuantificationSeverityPolicyTests
{
    [Theory]
    [InlineData(31, null, null, null, 5)]
    [InlineData(16, null, null, null, 4)]
    [InlineData(6, null, null, null, 3)]
    [InlineData(null, 21, null, null, 4)]
    [InlineData(null, 11, null, null, 3)]
    [InlineData(null, null, null, 51, 4)]
    [InlineData(null, null, null, 26, 3)]
    [InlineData(null, null, 51, null, 3)]
    [InlineData(null, null, 21, null, 2)]
    [InlineData(null, null, null, null, 2)]
    public void Estimate_applies_shared_quantification_priority(
        int? crossSectionReduction,
        int? intrusion,
        int? height,
        int? extent,
        int expected)
    {
        var actual = QuantificationSeverityPolicy.Estimate(
            crossSectionReduction,
            intrusion,
            height,
            extent);

        Assert.Equal(expected, actual);
    }
}
