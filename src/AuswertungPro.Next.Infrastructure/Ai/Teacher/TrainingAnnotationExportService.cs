using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

public sealed class TrainingAnnotationExportService(
    string imagesDir,
    string labelsDir,
    ITrainingImageCropper imageCropper) : ITrainingAnnotationExportService
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
            var cropsDir = Path.Combine(imagesDir, "crops");
            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(cropsDir);
            Directory.CreateDirectory(labelsDir);

            var fullFrameName = $"{baseName}.png";
            result.FullFramePath = Path.Combine(imagesDir, fullFrameName);
            File.Copy(sourceFramePath, result.FullFramePath, overwrite: true);

            var cropName = $"{baseName}_crop.png";
            result.CroppedRegionPath = Path.Combine(cropsDir, cropName);
            imageCropper.CropAndSave(sourceFramePath, bbox, result.CroppedRegionPath);

            var labelName = $"{baseName}.txt";
            result.YoloAnnotationPath = Path.Combine(labelsDir, labelName);
            await File.WriteAllTextAsync(result.YoloAnnotationPath, bbox.ToYoloLine(classId), ct);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}
