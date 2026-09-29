using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingAnnotationExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesFrameCropAndYoloLabel()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var imagesDir = Path.Combine(root, "teacher_images");
        var labelsDir = Path.Combine(root, "teacher_labels");
        var sourcePath = Path.Combine(root, "source.png");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(sourcePath, TransparentPng1x1);

            var service = new TrainingAnnotationExportService(
                imagesDir,
                labelsDir,
                new CopyCropper());

            var result = await service.ExportAsync(
                sourcePath,
                new NormalizedBoundingBox
                {
                    XCenter = 0.5,
                    YCenter = 0.5,
                    Width = 1.0,
                    Height = 1.0
                },
                "BAA",
                classId: 7,
                baseName: "sample");

            Assert.True(result.Success, result.Error ?? "Export failed without error message.");
            Assert.True(File.Exists(result.FullFramePath));
            Assert.True(File.Exists(result.CroppedRegionPath));
            Assert.True(File.Exists(result.YoloAnnotationPath));
            Assert.Equal("7 0.500000 0.500000 1.000000 1.000000", await File.ReadAllTextAsync(result.YoloAnnotationPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_FehlendeQuelle_meldet_deutschen_Grund_und_behaelt_die_Ausnahme()
    {
        // Aufgabe 10c2, Fix-Runde 2: Error wird im Pruefplatz angezeigt und war der
        // englische File.Copy-Text ("Could not find file …").
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new TrainingAnnotationExportService(
                Path.Combine(root, "teacher_images"), Path.Combine(root, "teacher_labels"), new CopyCropper());

            var result = await service.ExportAsync(
                Path.Combine(root, "fehlt.png"),
                new NormalizedBoundingBox { XCenter = 0.5, YCenter = 0.5, Width = 1.0, Height = 1.0 },
                "BAA", classId: 7, baseName: "sample");

            Assert.False(result.Success);
            Assert.IsType<FileNotFoundException>(result.Failure);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
            Assert.DoesNotContain("Could not find", result.Error);
            Assert.DoesNotContain(result.Failure!.Message, result.Error);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CopyCropper : ITrainingImageCropper
    {
        public void CropAndSave(string sourceFramePath, NormalizedBoundingBox bbox, string outputPath)
            => File.Copy(sourceFramePath, outputPath, overwrite: true);
    }

    private static readonly byte[] TransparentPng1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
