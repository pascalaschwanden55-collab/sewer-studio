using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingManualCalibrationResult(
    bool IsValid,
    PipeCalibration? Calibration,
    string StatusText,
    string HintText);

public static class CodingManualCalibrationPolicy
{
    private const double MinimumPixelDiameter = 10.0;

    public static CodingManualCalibrationResult Build(
        NormalizedPoint start,
        NormalizedPoint end,
        Point startPixel,
        Point endPixel,
        int nominalDiameterMm)
    {
        double pixelDiameter = Distance(startPixel, endPixel);
        if (pixelDiameter < MinimumPixelDiameter)
        {
            return new CodingManualCalibrationResult(
                IsValid: false,
                Calibration: null,
                StatusText: "",
                HintText: "Linie zu kurz - bitte nochmal");
        }

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        var calibration = new PipeCalibration
        {
            NominalDiameterMm = nominalDiameterMm,
            PipePixelDiameter = pixelDiameter,
            NormalizedDiameter = Math.Sqrt(dx * dx + dy * dy),
            PipeCenter = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2),
            WasManuallyCalibrated = true,
            Source = CalibrationSource.Manual
        };

        return new CodingManualCalibrationResult(
            IsValid: true,
            Calibration: calibration,
            StatusText: $"Kalibriert: {calibration.MmPerNormUnit:F1} mm/norm",
            HintText: $"Kalibriert! DN {nominalDiameterMm}mm = {pixelDiameter:F0}px");
    }

    private static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
