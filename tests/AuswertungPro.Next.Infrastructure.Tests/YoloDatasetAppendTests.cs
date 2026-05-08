using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation): Single-Sample-Append in den YOLO-seg-
/// Datensatz. Wichtig (Plan-Header B4):
///  - Pfad-Layout {root}/images/train/ + {root}/labels/train/ (NICHT
///    train/images, das war im Plan-Snippet vertauscht).
///  - Class-ID kommt aus VsaYoloClassMap.TryGetClassId (stabil), nicht aus
///    dem Code selbst.
///  - YOLO-seg-Label ist class_id + Polygon-Punkte (normiert 0..1).
/// </summary>
public sealed class YoloDatasetAppendTests : IDisposable
{
    private readonly string _datasetRoot;
    private readonly string _frameDir;

    public YoloDatasetAppendTests()
    {
        var unique = Guid.NewGuid().ToString("N");
        _datasetRoot = Path.Combine(Path.GetTempPath(), "YoloDatasetAppendTests-" + unique);
        _frameDir = Path.Combine(_datasetRoot, "_frames");
        Directory.CreateDirectory(_frameDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_datasetRoot)) Directory.Delete(_datasetRoot, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task AppendSampleAsync_KnownCode_WritesImageAndLabelInCorrectLayout()
    {
        var framePath = WriteDummyFrame("frame-1.png");
        var sample = MakeSample("sample-001", "BAB B", framePath);
        var preview = MakePreview(width: 1920, height: 1080,
            polygonPx: new[] { (200.0, 100.0), (1000.0, 100.0), (1000.0, 800.0), (200.0, 800.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);
        var labelPath = await svc.AppendSampleAsync(sample, preview, CancellationToken.None);

        // B4: Pfad-Layout images/train + labels/train (nicht train/images)
        var trainImgDir = Path.Combine(_datasetRoot, "images", "train");
        var trainLblDir = Path.Combine(_datasetRoot, "labels", "train");
        Assert.True(Directory.Exists(trainImgDir), "images/train/ fehlt");
        Assert.True(Directory.Exists(trainLblDir), "labels/train/ fehlt");

        var copiedImage = Directory.GetFiles(trainImgDir).Single();
        Assert.EndsWith(".png", copiedImage);

        Assert.Equal(trainLblDir, Path.GetDirectoryName(labelPath));
        Assert.True(File.Exists(labelPath));
    }

    [Fact]
    public async Task AppendSampleAsync_LabelFormat_IsYoloSegWithNormalizedPolygon()
    {
        var framePath = WriteDummyFrame("frame-2.png");
        var sample = MakeSample("sample-002", "BAB B", framePath);
        // 4-Punkt-Polygon in Pixelkoordinaten — normalisiert sollten das 0.1, 0.2 etc. werden
        var preview = MakePreview(width: 100, height: 200,
            polygonPx: new[] { (10.0, 40.0), (90.0, 40.0), (90.0, 160.0), (10.0, 160.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);
        var labelPath = await svc.AppendSampleAsync(sample, preview, CancellationToken.None);

        var content = (await File.ReadAllTextAsync(labelPath)).TrimEnd('\n', '\r');
        var tokens = content.Split(' ');

        // class_id + 8 Koordinaten (4 Punkte * 2)
        Assert.Equal(9, tokens.Length);

        // Erste Token = class_id (BAB ist im Default-Mapping = 4)
        Assert.Equal("4", tokens[0]);

        // Punkt 0: (10, 40) → (0.10, 0.20)
        Assert.Equal(0.10, double.Parse(tokens[1], CultureInfo.InvariantCulture), precision: 4);
        Assert.Equal(0.20, double.Parse(tokens[2], CultureInfo.InvariantCulture), precision: 4);

        // Punkt 2: (90, 160) → (0.90, 0.80)
        Assert.Equal(0.90, double.Parse(tokens[5], CultureInfo.InvariantCulture), precision: 4);
        Assert.Equal(0.80, double.Parse(tokens[6], CultureInfo.InvariantCulture), precision: 4);
    }

    [Fact]
    public async Task AppendSampleAsync_UnknownCode_ThrowsInsteadOfAutoCreate()
    {
        var framePath = WriteDummyFrame("frame-3.png");
        // QQQ ist garantiert nicht in VsaYoloClassMap-Defaults und ohne
        // Auto-Create darf der Writer keine Class-ID ausdenken.
        var sample = MakeSample("sample-003", "QQQ X", framePath);
        var preview = MakePreview(width: 1920, height: 1080,
            polygonPx: new[] { (10.0, 10.0), (20.0, 10.0), (20.0, 20.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AppendSampleAsync(sample, preview, CancellationToken.None));
    }

    [Fact]
    public async Task AppendSampleAsync_MissingFrame_Throws()
    {
        var sample = MakeSample("sample-004", "BAB B", framePath: Path.Combine(_frameDir, "missing.png"));
        var preview = MakePreview(width: 100, height: 100,
            polygonPx: new[] { (10.0, 10.0), (20.0, 10.0), (20.0, 20.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);

        await Assert.ThrowsAnyAsync<IOException>(() =>
            svc.AppendSampleAsync(sample, preview, CancellationToken.None));
    }

    [Fact]
    public async Task AppendSampleAsync_WritesDataYamlFromVsaYoloClassMap()
    {
        var framePath = WriteDummyFrame("frame-yaml.png");
        var sample = MakeSample("sample-yaml", "BAB B", framePath);
        var preview = MakePreview(width: 100, height: 100,
            polygonPx: new[] { (10.0, 10.0), (90.0, 10.0), (90.0, 90.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);
        await svc.AppendSampleAsync(sample, preview, CancellationToken.None);

        var yamlPath = Path.Combine(_datasetRoot, "data.yaml");
        Assert.True(File.Exists(yamlPath), "data.yaml fehlt — Dataset waere nicht trainierbar.");

        var lines = await File.ReadAllLinesAsync(yamlPath);
        Assert.Contains(lines, l => l.StartsWith("path:", StringComparison.Ordinal));
        Assert.Contains(lines, l => l == "train: images/train");
        Assert.Contains(lines, l => l == "val: images/val");
        Assert.Contains(lines, l => l.StartsWith("nc:", StringComparison.Ordinal));

        // Klassen-Liste muss die VsaYoloClassMap-Reihenfolge spiegeln (BCD an Index 0).
        var namesLine = lines.Single(l => l.StartsWith("names:", StringComparison.Ordinal));
        Assert.Contains("'BCD'", namesLine);
        Assert.Contains("'BAB'", namesLine);
    }

    [Fact]
    public async Task AppendSampleAsync_CreatesValDirectoriesEvenWithoutValSamples()
    {
        var framePath = WriteDummyFrame("frame-val.png");
        var sample = MakeSample("sample-val", "BAB B", framePath);
        var preview = MakePreview(width: 100, height: 100,
            polygonPx: new[] { (10.0, 10.0), (90.0, 10.0), (90.0, 90.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);
        await svc.AppendSampleAsync(sample, preview, CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(_datasetRoot, "images", "val")),
            "images/val/ fehlt — YOLO-Training crasht beim val-Split.");
        Assert.True(Directory.Exists(Path.Combine(_datasetRoot, "labels", "val")),
            "labels/val/ fehlt — YOLO-Training crasht beim val-Split.");
    }

    [Fact]
    public async Task AppendSampleAsync_TwoSamples_ProduceDistinctLabelFiles()
    {
        var framePath1 = WriteDummyFrame("frame-a.png");
        var framePath2 = WriteDummyFrame("frame-b.png");

        var preview = MakePreview(width: 100, height: 100,
            polygonPx: new[] { (10.0, 10.0), (90.0, 10.0), (90.0, 90.0) });

        var svc = new YoloDatasetExportService(_datasetRoot);
        var label1 = await svc.AppendSampleAsync(
            MakeSample("sample-a", "BAB B", framePath1), preview, CancellationToken.None);
        var label2 = await svc.AppendSampleAsync(
            MakeSample("sample-b", "BAB B", framePath2), preview, CancellationToken.None);

        Assert.NotEqual(label1, label2);
        Assert.True(File.Exists(label1));
        Assert.True(File.Exists(label2));
    }

    private string WriteDummyFrame(string name)
    {
        var path = Path.Combine(_frameDir, name);
        // Kein echtes PNG — der Writer kopiert die Datei nur, kein Decode.
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        return path;
    }

    private static TrainingSample MakeSample(string sampleId, string code, string framePath) => new()
    {
        SampleId = sampleId,
        CaseId = "case-1",
        Code = code,
        FramePath = framePath,
    };

    private static MaskPreview MakePreview(int width, int height, (double x, double y)[] polygonPx)
    {
        var json = "[" + string.Join(",",
            polygonPx.Select(p =>
                $"[{p.x.ToString(CultureInfo.InvariantCulture)},{p.y.ToString(CultureInfo.InvariantCulture)}]")) + "]";
        return new MaskPreview(
            SamMaskRle: "rle",
            SamMaskEncoding: "sidecar-sam-rle-v1",
            PolygonJson: json,
            MaskWidth: width,
            MaskHeight: height,
            MaskAreaPixels: 1234,
            SamConfidence: 0.85,
            SamLatency: TimeSpan.FromMilliseconds(120),
            Warnings: null);
    }
}
