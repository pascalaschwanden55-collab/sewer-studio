using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoResultWorkflowTests
{
    [Fact]
    public void Apply_uebernimmt_mapping_overlay_foto_und_kalibrierung()
    {
        var photoPaths = new List<string> { "old1.png" };
        var calibration = new PipeCalibration { NominalDiameterMm = 400, NormalizedDiameter = 0.7 };

        var applyResult = VsaCodeExplorerPhotoResultWorkflow.Apply(
            new VsaCodeExplorerPhotoResultApplyRequest(
                Result: new PhotoMeasurementResult
                {
                    Geometry = new OverlayGeometry { FillPercent = 33.3, ClockFrom = 6.8 },
                    OverlayPhotoPath = "overlay.png",
                    UpdatedCalibration = calibration
                },
                PhotoIndex: 1,
                PhotoPaths: photoPaths,
                FileExists: path => path == "overlay.png"));

        Assert.Equal("33.3", applyResult.Q1Value);
        Assert.Equal("06", applyResult.ClockVon);
        Assert.True(applyResult.PhotoPathChanged);
        Assert.Same(calibration, applyResult.UpdatedCalibration);
        Assert.Equal(["old1.png", "overlay.png"], photoPaths);
    }

    [Fact]
    public void Apply_ignoriert_overlay_foto_wenn_datei_fehlt()
    {
        var photoPaths = new List<string> { "old1.png", "old2.png" };

        var applyResult = VsaCodeExplorerPhotoResultWorkflow.Apply(
            new VsaCodeExplorerPhotoResultApplyRequest(
                Result: new PhotoMeasurementResult
                {
                    Geometry = new OverlayGeometry { Q1Mm = 12.3 },
                    OverlayPhotoPath = "missing.png"
                },
                PhotoIndex: 1,
                PhotoPaths: photoPaths,
                FileExists: _ => false));

        Assert.Equal("12.3", applyResult.Q1Value);
        Assert.Null(applyResult.ClockVon);
        Assert.False(applyResult.PhotoPathChanged);
        Assert.Null(applyResult.UpdatedCalibration);
        Assert.Equal(["old1.png", "old2.png"], photoPaths);
    }
}
