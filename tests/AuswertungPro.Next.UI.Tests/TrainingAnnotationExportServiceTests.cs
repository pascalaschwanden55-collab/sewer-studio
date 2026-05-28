using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Teacher;

namespace AuswertungPro.Next.UI.Tests;

[Collection("EnvironmentVars")]
public sealed class TrainingAnnotationExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WorksWithoutWpfApplication()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", root);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            var sourcePath = Path.Combine(root, "source.png");
            Directory.CreateDirectory(root);
            await File.WriteAllBytesAsync(sourcePath, TransparentPng1x1);

            var service = TrainingAnnotationExportServiceFactory.Create();

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

            Assert.NotNull(result);
            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Export failed without error message.");
            Assert.True(File.Exists(result.FullFramePath));
            Assert.True(File.Exists(result.CroppedRegionPath));
            Assert.True(File.Exists(result.YoloAnnotationPath));
            Assert.Equal("7 0.500000 0.500000 1.000000 1.000000", await File.ReadAllTextAsync(result.YoloAnnotationPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static readonly byte[] TransparentPng1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
