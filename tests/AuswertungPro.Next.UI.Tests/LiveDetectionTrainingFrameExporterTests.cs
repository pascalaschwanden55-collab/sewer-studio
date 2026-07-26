using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTrainingFrameExporterTests
{
    [Fact]
    public async Task ExportAsync_writes_frame_for_export_and_deletes_temp_file_afterwards()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tempPath = Path.Combine(root, "frame.png");
        var exportService = new RecordingExportService();
        var exporter = new LiveDetectionTrainingFrameExporter(exportService, _ => tempPath);
        var frameBytes = new byte[] { 1, 2, 3, 4 };
        var bbox = new NormalizedBoundingBox
        {
            XCenter = 0.5,
            YCenter = 0.6,
            Width = 0.2,
            Height = 0.3
        };

        try
        {
            var result = await exporter.ExportAsync(
                frameBytes,
                bbox,
                code: "BAA",
                classId: 7,
                baseName: "det_abc",
                annotationId: "abc");

            Assert.True(result.Success);
            Assert.Equal(tempPath, exportService.SourceFramePath);
            Assert.Equal(frameBytes, exportService.SourceBytes);
            Assert.Same(bbox, exportService.BoundingBox);
            Assert.Equal("BAA", exportService.Code);
            Assert.Equal(7, exportService.ClassId);
            Assert.Equal("det_abc", exportService.BaseName);
            Assert.False(File.Exists(tempPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_deletes_temp_file_when_export_fails()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tempPath = Path.Combine(root, "frame.png");
        var exportService = new RecordingExportService { ThrowOnExport = true };
        var exporter = new LiveDetectionTrainingFrameExporter(exportService, _ => tempPath);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                exporter.ExportAsync(
                    new byte[] { 9, 8, 7 },
                    new NormalizedBoundingBox(),
                    code: "BAA",
                    classId: 1,
                    baseName: "det_fail",
                    annotationId: "fail"));

            Assert.False(File.Exists(tempPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingExportService : ITrainingAnnotationExportService
    {
        public bool ThrowOnExport { get; init; }
        public string? SourceFramePath { get; private set; }
        public byte[]? SourceBytes { get; private set; }
        public NormalizedBoundingBox? BoundingBox { get; private set; }
        public string? Code { get; private set; }
        public int ClassId { get; private set; }
        public string? BaseName { get; private set; }

        public async Task<TrainingAnnotationResult> ExportAsync(
            string sourceFramePath,
            NormalizedBoundingBox bbox,
            string vsaCode,
            int classId,
            string baseName,
            CancellationToken ct = default)
        {
            SourceFramePath = sourceFramePath;
            SourceBytes = await File.ReadAllBytesAsync(sourceFramePath, ct);
            BoundingBox = bbox;
            Code = vsaCode;
            ClassId = classId;
            BaseName = baseName;

            if (ThrowOnExport)
                throw new InvalidOperationException("export failed");

            return new TrainingAnnotationResult
            {
                Success = true,
                FullFramePath = "full.png",
                CroppedRegionPath = "crop.png",
                YoloAnnotationPath = "label.txt"
            };
        }
    }
}
