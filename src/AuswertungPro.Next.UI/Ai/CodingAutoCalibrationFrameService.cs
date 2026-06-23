using System;
using System.IO;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingAutoCalibrationFrameService
{
    public static PipeCalibration? TryAutoCalibrate(
        byte[]? frameBytes,
        int nominalDiameterMm,
        Func<byte[], BitmapSource>? bitmapLoader = null,
        Func<BitmapSource, int, PipeCalibration?>? autoCalibrate = null)
    {
        if (frameBytes == null || frameBytes.Length == 0)
            return null;

        bitmapLoader ??= LoadBitmap;
        autoCalibrate ??= AutoCalibrationService.TryAutoCalibrate;

        return autoCalibrate(bitmapLoader(frameBytes), nominalDiameterMm);
    }

    private static BitmapSource LoadBitmap(byte[] frameBytes)
    {
        using var stream = new MemoryStream(frameBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
