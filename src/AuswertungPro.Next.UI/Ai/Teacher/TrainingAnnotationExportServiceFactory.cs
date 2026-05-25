using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai.Teacher;

public static class TrainingAnnotationExportServiceFactory
{
    public static ITrainingAnnotationExportService Create()
        => new TrainingAnnotationExportService(
            TeacherAnnotationStore.GetImagesDir(),
            TeacherAnnotationStore.GetLabelsDir(),
            new WpfTrainingImageCropper());
}

internal sealed class WpfTrainingImageCropper : ITrainingImageCropper
{
    public void CropAndSave(string sourceFramePath, NormalizedBoundingBox bbox, string outputPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(sourceFramePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        var imgWidth = bitmap.PixelWidth;
        var imgHeight = bitmap.PixelHeight;

        var cropX = (int)Math.Max(0, (bbox.XCenter - bbox.Width / 2) * imgWidth);
        var cropY = (int)Math.Max(0, (bbox.YCenter - bbox.Height / 2) * imgHeight);
        var cropW = (int)Math.Min(imgWidth - cropX, bbox.Width * imgWidth);
        var cropH = (int)Math.Min(imgHeight - cropY, bbox.Height * imgHeight);

        if (cropW <= 0 || cropH <= 0)
        {
            File.Copy(sourceFramePath, outputPath, overwrite: true);
            return;
        }

        var cropped = new CroppedBitmap(bitmap, new Int32Rect(cropX, cropY, cropW, cropH));
        cropped.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cropped));

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
