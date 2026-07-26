using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTrainingAnnotationWriterTests
{
    [Fact]
    public async Task SaveAcceptedAsync_exports_frame_builds_detection_annotation_and_appends_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tempPath = Path.Combine(root, "frame.png");
        var exportService = new RecordingExportService();
        var appended = new List<TeacherAnnotation>();
        var writer = new LiveDetectionTrainingAnnotationWriter(
            new LiveDetectionTrainingFrameExporter(exportService, _ => tempPath),
            () => "abc123",
            annotation =>
            {
                appended.Add(annotation);
                return Task.CompletedTask;
            },
            exportPlanner: CreatePlanner());

        try
        {
            var finding = new LiveFrameFinding("Fallback", 3, "12", 25, VsaCodeHint: "BAB", WidthMm: 80);
            var frameBytes = new byte[] { 1, 2, 3 };

            var annotation = await writer.SaveAcceptedAsync(frameBytes, finding, TimeSpan.FromSeconds(12));

            Assert.Same(annotation, Assert.Single(appended));
            Assert.Equal("abc123", annotation.AnnotationId);
            Assert.Equal("BAB", annotation.VsaCode);
            Assert.Equal("Fallback", annotation.Beschreibung);
            Assert.Equal(TimeSpan.FromSeconds(12), annotation.VideoTimestamp);
            Assert.Equal(80, annotation.WidthMm);
            Assert.Equal("full.png", annotation.FullFramePath);
            Assert.Equal(frameBytes, exportService.SourceBytes);
            Assert.Equal("BAB", exportService.Code);
            Assert.Equal(42, exportService.ClassId);
            Assert.Equal("det_abc123", exportService.BaseName);
            Assert.Same(annotation.BoundingBox, exportService.BoundingBox);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCorrectedAsync_uses_selected_entry_code_description_and_corrected_export_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tempPath = Path.Combine(root, "frame.png");
        var exportService = new RecordingExportService();
        var appended = new List<TeacherAnnotation>();
        var writer = new LiveDetectionTrainingAnnotationWriter(
            new LiveDetectionTrainingFrameExporter(exportService, _ => tempPath),
            () => "corr789",
            annotation =>
            {
                appended.Add(annotation);
                return Task.CompletedTask;
            },
            exportPlanner: CreatePlanner());

        try
        {
            var finding = new LiveFrameFinding("KI-Vorschlag", 2, "3", 20, VsaCodeHint: "BAB");
            var selectedEntry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" };

            var annotation = await writer.SaveCorrectedAsync(
                new byte[] { 9, 8, 7 },
                finding,
                selectedEntry,
                TimeSpan.FromSeconds(44));

            Assert.Same(annotation, Assert.Single(appended));
            Assert.Equal("corr789", annotation.AnnotationId);
            Assert.Equal("BCA", annotation.VsaCode);
            Assert.Equal("Anschluss", annotation.Beschreibung);
            Assert.Equal(TimeSpan.FromSeconds(44), annotation.VideoTimestamp);
            Assert.Equal("BCA", exportService.Code);
            Assert.Equal("det_corr_corr789", exportService.BaseName);
            Assert.Same(annotation.BoundingBox, exportService.BoundingBox);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveManualMarkAsync_exports_overlay_bbox_builds_manual_mark_annotation_and_appends_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tempPath = Path.Combine(root, "frame.png");
        var exportService = new RecordingExportService();
        var appended = new List<TeacherAnnotation>();
        var writer = new LiveDetectionTrainingAnnotationWriter(
            new LiveDetectionTrainingFrameExporter(exportService, _ => tempPath),
            () => "mark123",
            annotation =>
            {
                appended.Add(annotation);
                return Task.CompletedTask;
            },
            exportPlanner: CreatePlanner());

        try
        {
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points =
                [
                    new NormalizedPoint(0.2, 0.3),
                    new NormalizedPoint(0.6, 0.7)
                ],
                Q1Mm = 11,
                Q2Mm = 22
            };
            var selectedEntry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" };
            var frameBytes = new byte[] { 4, 5, 6 };

            var annotation = await writer.SaveManualMarkAsync(
                frameBytes,
                selectedEntry,
                overlay,
                clockPosition: "3",
                captureMeter: 7.5,
                videoTimestamp: TimeSpan.FromSeconds(33));

            Assert.NotNull(annotation);
            Assert.Same(annotation, Assert.Single(appended));
            Assert.Equal("mark123", annotation!.AnnotationId);
            Assert.Equal("BCA", annotation.VsaCode);
            Assert.Equal("Anschluss", annotation.Beschreibung);
            Assert.Equal(7.5, annotation.MeterPosition);
            Assert.Equal(TimeSpan.FromSeconds(33), annotation.VideoTimestamp);
            Assert.Equal(OverlayToolType.Rectangle, annotation.ToolType);
            Assert.Equal(3, annotation.ClockPosition);
            Assert.Equal(11, annotation.HeightMm);
            Assert.Equal(22, annotation.WidthMm);
            Assert.Equal("full.png", annotation.FullFramePath);
            Assert.Equal(frameBytes, exportService.SourceBytes);
            Assert.Equal("BCA", exportService.Code);
            Assert.Equal("mark_mark123", exportService.BaseName);
            Assert.Same(annotation.BoundingBox, exportService.BoundingBox);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveManualMarkAsync_returns_null_without_export_for_tiny_bbox()
    {
        var exportService = new RecordingExportService();
        var writer = new LiveDetectionTrainingAnnotationWriter(
            new LiveDetectionTrainingFrameExporter(exportService),
            () => "mark123",
            _ => throw new InvalidOperationException("Annotation must not be appended."),
            CreatePlanner());
        var overlay = new OverlayGeometry
        {
            Points =
            [
                new NormalizedPoint(0.4, 0.4),
                new NormalizedPoint(0.4, 0.4)
            ]
        };

        var annotation = await writer.SaveManualMarkAsync(
            new byte[] { 1 },
            new ProtocolEntry { Code = "BCA" },
            overlay,
            clockPosition: null,
            captureMeter: 0,
            videoTimestamp: TimeSpan.Zero);

        Assert.Null(annotation);
        Assert.Equal(0, exportService.ExportCount);
    }

    private sealed class RecordingExportService : ITrainingAnnotationExportService
    {
        public int ExportCount { get; private set; }
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
            ExportCount++;
            SourceBytes = await File.ReadAllBytesAsync(sourceFramePath, ct);
            BoundingBox = bbox;
            Code = vsaCode;
            ClassId = classId;
            BaseName = baseName;

            return new TrainingAnnotationResult
            {
                Success = true,
                FullFramePath = "full.png",
                CroppedRegionPath = "crop.png",
                YoloAnnotationPath = "label.txt"
            };
        }
    }

    private static LiveDetectionTrainingExportPlanner CreatePlanner()
        => new(new FixedClassMap());

    private sealed class FixedClassMap : IVsaYoloClassMapStore
    {
        public int GetClassId(string vsaCode)
            => throw new InvalidOperationException("Der Live-Teacher-Weg muss GetOrAddClassId verwenden.");

        public int GetOrAddClassId(string vsaCode) => 42;

        public Dictionary<string, int> GetFullMap() => new() { ["TEST"] = 42 };

        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }
}
