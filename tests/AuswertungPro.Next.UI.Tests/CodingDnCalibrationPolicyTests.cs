using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDnCalibrationPolicyTests
{
    [Fact]
    public void Build_creates_nominal_calibration_from_positive_dn_field()
    {
        var state = CodingDnCalibrationPolicy.Build(new Dictionary<string, string>
        {
            ["DN_mm"] = "300"
        });

        Assert.Equal(300, state.NominalDiameterMm);
        Assert.NotNull(state.Calibration);
        Assert.Equal(300, state.Calibration.NominalDiameterMm);
        Assert.Equal("DN: 300 mm", state.DnText);
        Assert.Equal("Nicht kalibriert", state.CalibrationStatusText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("DN300")]
    public void Build_keeps_unknown_state_for_missing_or_invalid_dn(string? rawDn)
    {
        var fields = rawDn is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["DN_mm"] = rawDn };

        var state = CodingDnCalibrationPolicy.Build(fields);

        Assert.Equal(0, state.NominalDiameterMm);
        Assert.Null(state.Calibration);
        Assert.Equal("DN: unbekannt", state.DnText);
        Assert.Equal("Nicht kalibriert", state.CalibrationStatusText);
    }
}
