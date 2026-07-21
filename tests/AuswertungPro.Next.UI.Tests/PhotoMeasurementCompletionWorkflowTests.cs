using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.PhotoMeasurement;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoMeasurementCompletionWorkflowTests
{
    [Fact]
    public void Execute_without_geometry_confirms_result_without_running_export_actions()
    {
        var calibration = new PipeCalibration { NominalDiameterMm = 300 };
        var calls = new List<string>();

        var result = Execute(
            geometry: null,
            calibration,
            export: () =>
            {
                calls.Add("export");
                return "overlay.png";
            },
            describeError: _ =>
            {
                calls.Add("describe");
                return "error";
            },
            showStatus: _ => calls.Add("status"));

        Assert.True(result.Confirmed);
        Assert.Null(result.Geometry);
        Assert.Null(result.OverlayPhotoPath);
        Assert.Same(calibration, result.UpdatedCalibration);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_with_geometry_returns_the_exact_export_path_and_references()
    {
        var geometry = new OverlayGeometry { ToolType = OverlayToolType.Line };
        var calibration = new PipeCalibration { NominalDiameterMm = 400 };
        var exportCalls = 0;

        var result = Execute(
            geometry,
            calibration,
            export: () =>
            {
                exportCalls++;
                return "C:\\photos\\measurement_overlay.png";
            });

        Assert.Equal(1, exportCalls);
        Assert.True(result.Confirmed);
        Assert.Same(geometry, result.Geometry);
        Assert.Equal("C:\\photos\\measurement_overlay.png", result.OverlayPhotoPath);
        Assert.Same(calibration, result.UpdatedCalibration);
    }

    [Fact]
    public void Execute_when_export_returns_null_confirms_without_reporting_an_error()
    {
        var calls = new List<string>();

        var result = Execute(
            new OverlayGeometry(),
            new PipeCalibration(),
            export: () => null,
            describeError: _ =>
            {
                calls.Add("describe");
                return "error";
            },
            showStatus: _ => calls.Add("status"));

        Assert.True(result.Confirmed);
        Assert.Null(result.OverlayPhotoPath);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_when_export_throws_reports_the_error_and_still_confirms_the_measurement()
    {
        var geometry = new OverlayGeometry { ToolType = OverlayToolType.CrossSection };
        var calibration = new PipeCalibration { NominalDiameterMm = 500 };
        var failure = new IOException("locked");
        Exception? describedException = null;
        string? shownStatus = null;

        var result = Execute(
            geometry,
            calibration,
            export: () => throw failure,
            describeError: ex =>
            {
                describedException = ex;
                return "Datei gesperrt";
            },
            showStatus: status => shownStatus = status);

        Assert.Same(failure, describedException);
        Assert.Equal("Overlay-Export fehlgeschlagen: Datei gesperrt", shownStatus);
        Assert.True(result.Confirmed);
        Assert.Same(geometry, result.Geometry);
        Assert.Null(result.OverlayPhotoPath);
        Assert.Same(calibration, result.UpdatedCalibration);
    }

    private static PhotoMeasurementResult Execute(
        OverlayGeometry? geometry,
        PipeCalibration calibration,
        Func<string?> export,
        Func<Exception, string>? describeError = null,
        Action<string>? showStatus = null)
        => PhotoMeasurementCompletionWorkflow.Execute(
            new PhotoMeasurementCompletionRequest(geometry, calibration),
            new PhotoMeasurementCompletionActions(
                export,
                describeError ?? (_ => "error"),
                showStatus ?? (_ => { })));
}
