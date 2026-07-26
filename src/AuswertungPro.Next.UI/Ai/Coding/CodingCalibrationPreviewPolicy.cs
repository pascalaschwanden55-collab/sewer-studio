using System.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingCalibrationPreviewState(
    Point Start,
    Point End,
    double PixelLength,
    string HintText);

public static class CodingCalibrationPreviewPolicy
{
    public static CodingCalibrationPreviewState Build(Point start, Point end)
    {
        var pixelLength = Math.Sqrt(
            Math.Pow(end.X - start.X, 2)
            + Math.Pow(end.Y - start.Y, 2));

        return new CodingCalibrationPreviewState(
            start,
            end,
            pixelLength,
            $"Referenzlinie: {pixelLength:F0} px");
    }
}
