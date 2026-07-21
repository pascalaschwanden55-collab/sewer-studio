using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.PhotoMeasurement;

internal sealed record PhotoMeasurementCompletionRequest(
    OverlayGeometry? Geometry,
    PipeCalibration Calibration);

internal sealed record PhotoMeasurementCompletionActions(
    Func<string?> ExportOverlayPhoto,
    Func<Exception, string> DescribeExportError,
    Action<string> ShowStatus);

internal static class PhotoMeasurementCompletionWorkflow
{
    public static PhotoMeasurementResult Execute(
        PhotoMeasurementCompletionRequest request,
        PhotoMeasurementCompletionActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Calibration);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ExportOverlayPhoto);
        ArgumentNullException.ThrowIfNull(actions.DescribeExportError);
        ArgumentNullException.ThrowIfNull(actions.ShowStatus);

        string? overlayPath = null;
        if (request.Geometry is not null)
        {
            try
            {
                overlayPath = actions.ExportOverlayPhoto();
            }
            catch (Exception ex)
            {
                actions.ShowStatus(
                    "Overlay-Export fehlgeschlagen: " + actions.DescribeExportError(ex));
            }
        }

        return new PhotoMeasurementResult
        {
            Geometry = request.Geometry,
            OverlayPhotoPath = overlayPath,
            Confirmed = true,
            UpdatedCalibration = request.Calibration
        };
    }
}
