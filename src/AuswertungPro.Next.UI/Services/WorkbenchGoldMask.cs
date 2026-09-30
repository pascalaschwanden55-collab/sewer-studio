using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Entscheidet, ob eine SAM-Maske ein Goldsample tragen darf, und uebertraegt nur eine
/// gepruefte Maske auf das Sample. Verlangt mehr als eine vorhandene Maske: die Masse
/// muessen zum gespeicherten Goldbild passen, das RLE muss lesbar, nicht leer, zur Box
/// passend und nicht degradiert sein, und die Flaeche wird aus dem RLE gezaehlt statt
/// dem Sidecarwert zu vertrauen.
/// </summary>
internal sealed class WorkbenchGoldMask : IGoldSampleMaske
{
    private static readonly WorkbenchGoldMask Invalid = new(null, null);

    private readonly WorkbenchSegmentation? _segmentation;

    private WorkbenchGoldMask(WorkbenchSegmentation? segmentation, int? areaPixels)
    {
        _segmentation = segmentation;
        AreaPixels = areaPixels;
    }

    public bool IsValid => _segmentation is not null;

    public int? AreaPixels { get; }

    public static WorkbenchGoldMask Evaluate(
        WorkbenchSegmentation? segmentation,
        BoundingBox box,
        string storedFramePath,
        Func<string, (int Width, int Height)?> readImageDimensions)
    {
        if (segmentation is null)
            return Invalid;

        bool dimensionsMatch;
        try
        {
            dimensionsMatch = readImageDimensions(storedFramePath) is { } dimensions
                              && dimensions.Width == segmentation.MaskImageWidth
                              && dimensions.Height == segmentation.MaskImageHeight;
        }
        catch
        {
            // Unlesbare oder widerspruechliche Bildmasse duerfen nie Gold ergeben, auch bei Abbruch.
            dimensionsMatch = false;
        }

        var valid = dimensionsMatch && SamMaskValidator.IsValid(
            segmentation.MaskRle,
            segmentation.MaskImageWidth,
            segmentation.MaskImageHeight,
            box,
            segmentation.Degraded,
            out _);
        if (!valid)
            return Invalid;

        return SamMaskFormatValidator.TryGetForegroundPixelCount(
            segmentation.MaskRle,
            segmentation.MaskImageWidth,
            segmentation.MaskImageHeight,
            out var foregroundPixelCount,
            out _)
            ? new WorkbenchGoldMask(segmentation, foregroundPixelCount)
            : Invalid;
    }

    /// <summary>
    /// Eine abgelehnte Maske bleibt bewusst weg, damit der Entwurf ueber !HasSamMask in
    /// 'Unvollstaendige Goldframes' erscheint und dort neu segmentiert wird.
    /// </summary>
    public void ApplyTo(TrainingSample sample)
    {
        if (_segmentation is null)
            return;

        sample.SamMaskRle = _segmentation.MaskRle;
        sample.SamMaskImageWidth = _segmentation.MaskImageWidth;
        sample.SamMaskImageHeight = _segmentation.MaskImageHeight;
        sample.SamMaskAreaPixels = AreaPixels;
        sample.SamMaskConfidence = _segmentation.Confidence;
        sample.SamMaskLabel = _segmentation.Label;
    }
}
