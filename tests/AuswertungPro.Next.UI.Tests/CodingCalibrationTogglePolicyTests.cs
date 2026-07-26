using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationTogglePolicyTests
{
    [Fact]
    public void Build_enables_calibration_from_inactive_state()
    {
        var state = CodingCalibrationTogglePolicy.Build(isCurrentlyCalibrating: false);

        Assert.True(state.IsCalibrating);
        Assert.Equal(OverlayToolType.None, state.ActiveTool);
        Assert.Equal("BtnCodingCalibrate", state.ActiveToolName);
        Assert.Equal("Kalibrieren", state.ToolLabel);
        Assert.True(state.ShowHint);
        Assert.Equal("Linie ueber den sichtbaren Rohrdurchmesser zeichnen", state.HintText);
    }

    [Fact]
    public void Build_disables_calibration_from_active_state()
    {
        var state = CodingCalibrationTogglePolicy.Build(isCurrentlyCalibrating: true);

        Assert.False(state.IsCalibrating);
        Assert.Equal(OverlayToolType.None, state.ActiveTool);
        Assert.Null(state.ActiveToolName);
        Assert.Equal("", state.ToolLabel);
        Assert.False(state.ShowHint);
        Assert.Equal("Linie ueber den sichtbaren Rohrdurchmesser zeichnen", state.HintText);
    }
}
