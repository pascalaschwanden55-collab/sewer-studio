using System.IO;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerPhotoResultApplyRequest(
    PhotoMeasurementResult Result,
    int PhotoIndex,
    IList<string> PhotoPaths,
    Func<string, bool> FileExists);

public sealed record VsaCodeExplorerPhotoResultApplyResult(
    string? Q1Value,
    string? ClockVon,
    bool PhotoPathChanged,
    PipeCalibration? UpdatedCalibration);

public static class VsaCodeExplorerPhotoResultWorkflow
{
    public static VsaCodeExplorerPhotoResultApplyResult Apply(
        VsaCodeExplorerPhotoResultApplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(request.PhotoPaths);
        ArgumentNullException.ThrowIfNull(request.FileExists);

        var mapped = PhotoMeasurementResultMapper.Map(request.Result);
        var photoPathChanged = TryApplyOverlayPhoto(
            request.PhotoPaths,
            request.PhotoIndex,
            request.Result.OverlayPhotoPath,
            request.FileExists);

        return new VsaCodeExplorerPhotoResultApplyResult(
            mapped.Q1Value,
            mapped.ClockVon,
            photoPathChanged,
            request.Result.UpdatedCalibration);
    }

    public static VsaCodeExplorerPhotoResultApplyResult Apply(
        PhotoMeasurementResult result,
        int photoIndex,
        IList<string> photoPaths)
        => Apply(
            new VsaCodeExplorerPhotoResultApplyRequest(
                result,
                photoIndex,
                photoPaths,
                File.Exists));

    private static bool TryApplyOverlayPhoto(
        IList<string> photoPaths,
        int photoIndex,
        string? overlayPhotoPath,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrEmpty(overlayPhotoPath) || !fileExists(overlayPhotoPath))
            return false;

        while (photoPaths.Count <= photoIndex)
            photoPaths.Add("");

        photoPaths[photoIndex] = overlayPhotoPath;
        return true;
    }
}
