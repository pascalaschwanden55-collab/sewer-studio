using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationStateControllerTests
{
    [Fact]
    public void Start_point_is_tracked_until_cleared()
    {
        var controller = new CodingCalibrationStateController();
        var start = new NormalizedPoint(0.25, 0.75);

        controller.SetStart(start);

        Assert.Equal(start, controller.Start);

        controller.ClearStart();

        Assert.Null(controller.Start);
    }

    [Fact]
    public void Reset_leaves_calibration_mode_and_clears_start()
    {
        var controller = new CodingCalibrationStateController();
        controller.SetCalibrating(true);
        controller.SetStart(new NormalizedPoint(0.1, 0.2));

        controller.Reset();

        Assert.False(controller.IsCalibrating);
        Assert.Null(controller.Start);
    }
}
