using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelClassifierInputPolicyTests
{
    [Fact]
    public void Build_uses_default_dn_when_calibration_has_no_nominal_diameter()
    {
        var input = CodingMultiModelClassifierInputPolicy.Build(
            nominalDiameterMm: null,
            currentMeter: 4.2,
            endMeter: 12.0);

        Assert.Equal(300, input.NominalDiameterMm);
        Assert.Equal(4.2, input.CurrentMeter);
        Assert.Equal(12.0, input.ReachLength);
    }

    [Fact]
    public void Build_keeps_calibrated_dn()
    {
        var input = CodingMultiModelClassifierInputPolicy.Build(
            nominalDiameterMm: 500,
            currentMeter: 1.4,
            endMeter: 8.0);

        Assert.Equal(500, input.NominalDiameterMm);
    }

    [Theory]
    [InlineData(null, 0.2, 1.0)]
    [InlineData(0.0, 3.4, 3.4)]
    [InlineData(-2.0, 0.6, 1.0)]
    public void Build_falls_back_to_current_meter_or_one_meter_when_end_meter_is_missing(
        double? endMeter,
        double currentMeter,
        double expectedReachLength)
    {
        var input = CodingMultiModelClassifierInputPolicy.Build(
            nominalDiameterMm: 300,
            currentMeter,
            endMeter);

        Assert.Equal(expectedReachLength, input.ReachLength);
    }
}
