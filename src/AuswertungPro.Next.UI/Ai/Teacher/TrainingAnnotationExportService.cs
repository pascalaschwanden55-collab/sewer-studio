using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai.Teacher;

/// <summary>
/// Exportiert Lehrer-Annotationen als YOLO-Trainingsdaten:
///  - Volles Frame nach teacher_images/
///  - Crop nach teacher_images/crops/
///  - YOLO .txt nach teacher_labels/
/// Verwendet WPF BitmapImage/CroppedBitmap (kein zusaetzliches NuGet).
/// </summary>
public sealed class TrainingAnnotationExportService : ITrainingAnnotationExportService
{
    public async Task<TrainingAnnotationResult> ExportAsync(
        string sourceFramePath,
        NormalizedBoundingBox bbox,
        string vsaCode,
        int classId,
        string baseName,
        CancellationToken ct = default)
    {
        var result = new TrainingAnnotationResult();

        try
        {
            var imagesDir = TeacherAnnotationStore.GetImagesDir();
            var cropsDir = Path.Combine(imagesDir, "crops");
            var labelsDir = TeacherAnnotationStore.GetLabelsDir();
            Directory.CreateDirectory(cropsDir);

            // 1. Volles Frame kopieren
            var fullFrameName = $"{baseName}.png";
            result.FullFramePath = Path.Combine(imagesDir, fullFrameName);
            File.Copy(sourceFramePath, result.FullFramePath, overwrite: true);

            // 2. Crop ausschneiden (muss auf UI-Thread laufen wegen WPF BitmapImage)
            var cropName = $"{baseName}_crop.png";
            result.CroppedRegionPath = Path.Combine(cropsDir, cropName);

            // WPF Bitmap-Operationen muessen ggf. auf dem UI-Thread laufen
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CropAndSave(sourceFramePath, bbox, result.CroppedRegionPath);
            });

            // 3. YOLO-Annotation schreiben
            var labelName = $"{baseName}.txt";
            result.YoloAnnotationPath = Path.Combine(labelsDir, labelName);
            var yoloLine = $"{classId} {bbox.XCenter:F6} {bbox.YCenter:F6} {bbox.Width:F6} {bbox.Height:F6}";
            await File.WriteAllTextAsync(result.YoloAnnotationPath, yoloLine, ct);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Schneidet den BoundingBox-Bereich aus dem Quellbild aus und speichert als PNG.
    /// </summary>
    private static void CropAndSave(string sourcePath, NormalizedBoundingBox bbox, string outputPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        int imgWidth = bitmap.PixelWidth;
        int imgHeight = bitmap.PixelHeight;

        // Normalisierte BBox → Pixel-Koordinaten
        int cropX = (int)Math.Max(0, (bbox.XCenter - bbox.Width / 2) * imgWidth);
        int cropY = (int)Math.Max(0, (bbox.YCenter - bbox.Height / 2) * imgHeight);
        int cropW = (int)Math.Min(imgWidth - cropX, bbox.Width * imgWidth);
        int cropH = (int)Math.Min(imgHeight - cropY, bbox.Height * imgHeight);

        if (cropW <= 0 || cropH <= 0)
        {
            // Fallback: ganzes Bild kopieren wenn BBox ungueltig
            File.Copy(sourcePath, outputPath, overwrite: true);
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
