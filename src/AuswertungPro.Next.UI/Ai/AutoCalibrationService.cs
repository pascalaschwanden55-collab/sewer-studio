using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Calibration;
using InfraAutoCalibrationService = AuswertungPro.Next.Infrastructure.Ai.Calibration.AutoCalibrationService;

namespace AuswertungPro.Next.UI.Ai;

public static class AutoCalibrationService
{
    public static PipeCalibration? TryAutoCalibrate(BitmapSource frame, int nominalDiameterMm)
    {
        if (frame is null)
            return null;

        var grayscaleFrame = ConvertToGrayscaleFrame(frame);
        return InfraAutoCalibrationService.TryAutoCalibrate(grayscaleFrame, nominalDiameterMm);
    }

    private static GrayscaleImageFrame ConvertToGrayscaleFrame(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;

        if (source.Format != PixelFormats.Bgr32 &&
            source.Format != PixelFormats.Bgra32 &&
            source.Format != PixelFormats.Pbgra32)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
            converted.Freeze();
            source = converted;
        }

        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var gray = new byte[width * height];
        for (var i = 0; i < width * height; i++)
        {
            var offset = i * 4;
            var b = pixels[offset];
            var g = pixels[offset + 1];
            var r = pixels[offset + 2];
            gray[i] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
        }

        return new GrayscaleImageFrame(width, height, gray);
    }
}
