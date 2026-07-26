using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

/// <summary>
/// Verhaltenstests fuer den Negativ-Pool-Anschluss (Trainingsplan D.3): kuratierte
/// schadensfreie Bilder mit bewusst leerer Labeldatei im plan-gesteuerten Detect-Export.
/// Die goldene Fixture bleibt unberuehrt — Plaene ohne Negative sind byteweise identisch.
/// </summary>
public sealed class TrainingExportNegativeImagesTests : IDisposable
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-negative-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Plan_ohne_Negative_bleibt_serialisierung_identisch()
    {
        var source = CreateFile("frame.png", PngBytes);
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);

        Assert.All(bundle.Plan.Images, image => Assert.False(image.IsNegative));
        var manifest = Encoding.UTF8.GetString(
            TrainingExportPlanSerializer.SerializeManifest(bundle.Plan));
        Assert.DoesNotContain("is_negative", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_mit_zwei_Negativen_hat_leere_Labels_und_deterministischen_Split()
    {
        var source = CreateFile("frame.png", PngBytes);
        var negativeA = CreateNegative("neg_a.bmp", BmpBytes(0x11));
        var negativeB = CreateNegative("neg_b.bmp", BmpBytes(0x22));

        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, [negativeA, negativeB]);

        Assert.Equal(3, bundle.Plan.Images.Count);
        var negatives = bundle.Plan.Images
            .Where(image => image.IsNegative)
            .OrderBy(image => image.ImageSha256, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, negatives.Length);
        Assert.All(negatives, image => Assert.Empty(image.Labels));
        Assert.All(negatives, image =>
            Assert.Equal(TrainingExportNegativePool.HoldingKey, image.HoldingKey));
        Assert.All(negatives, image =>
            Assert.StartsWith($"img_{image.ImageSha256}.", image.TargetFileName, StringComparison.OrdinalIgnoreCase));

        // Manifest und plan_id decken die Negative mit ab.
        var manifest = Encoding.UTF8.GetString(
            TrainingExportPlanSerializer.SerializeManifest(bundle.Plan));
        Assert.Equal(2, CountOccurrences(manifest, "\"is_negative\": true"));

        // Deterministik: gleicher Input -> gleicher Plan, gleiche Split-Zuordnung.
        var again = CreateBundle(source, TrainingExportHoldingRole.Train, [negativeA, negativeB]);
        Assert.Equal(bundle.Plan.PlanId, again.Plan.PlanId);
        Assert.Equal(
            negatives.Select(image => image.Target).ToArray(),
            again.Plan.Images
                .Where(image => image.IsNegative)
                .OrderBy(image => image.ImageSha256, StringComparer.Ordinal)
                .Select(image => image.Target)
                .ToArray());
    }

    [Fact]
    public async Task LocalExecutor_schreibt_0_Byte_Labels_fuer_Negative_und_ist_idempotent()
    {
        var source = CreateFile("frame.png", PngBytes);
        var negativeA = CreateNegative("neg_a.bmp", BmpBytes(0x11));
        var negativeB = CreateNegative("neg_b.bmp", BmpBytes(0x22));
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, [negativeA, negativeB]);
        var executor = new TrainingExportPlanLocalExecutor();
        var datasetRoot = Path.Combine(_root, "datasets");

        var result = await executor.ExecuteAsync(bundle, datasetRoot);

        Assert.Equal(TrainingExportExecutionStatus.Created, result.Status);
        foreach (var image in bundle.Plan.Images.Where(image => image.IsNegative))
        {
            var split = image.Target == TrainingExportTarget.Train ? "train" : "val";
            var labelPath = Path.Combine(
                result.DatasetPath,
                "labels",
                split,
                $"{Path.GetFileNameWithoutExtension(image.TargetFileName)}.txt");
            Assert.True(File.Exists(labelPath), $"Leere Labeldatei fehlt: {labelPath}");
            Assert.Equal(0, new FileInfo(labelPath).Length);
        }

        // Idempotenz prueft auch die Negativ-Dateien (AlreadyComplete, kein erneutes Schreiben).
        var second = await executor.ExecuteAsync(bundle, datasetRoot);
        Assert.Equal(TrainingExportExecutionStatus.AlreadyComplete, second.Status);
    }

    [Fact]
    public void Eval_kontaminiertes_Negativ_stoppt_den_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var contaminated = CreateNegative("eval_neg.bmp", BmpBytes(0x33));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(
                source,
                TrainingExportHoldingRole.Train,
                [contaminated],
                protectedImageHashes: new HashSet<string> { contaminated.Sha256 }));

        Assert.Contains("Eval", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kollision_positiv_und_negativ_stoppt_den_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var positiveHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source)));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(
                source,
                TrainingExportHoldingRole.Train,
                [new TrainingExportNegativeImage(source, positiveHash, null)]));

        Assert.Contains("Kollision", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SidecarRequest_packt_Negative_mit_leerem_Label_Array()
    {
        var source = CreateFile("frame.png", PngBytes);
        var negativeA = CreateNegative("neg_a.bmp", BmpBytes(0x11));
        var negativeB = CreateNegative("neg_b.bmp", BmpBytes(0x22));
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, [negativeA, negativeB]);

        var request = await new TrainingExportSidecarRequestBuilder().BuildAsync(bundle);

        Assert.Equal(3, request.Samples.Count);
        var negativeSamples = request.Samples
            .Where(sample => sample.Labels.Count == 0)
            .ToArray();
        Assert.Equal(2, negativeSamples.Length);
        Assert.All(negativeSamples, sample =>
            Assert.StartsWith($"img_{sample.ImageSha256}.", sample.TargetFileName, StringComparison.OrdinalIgnoreCase));
        // Positive behalten ihr Label.
        Assert.Single(request.Samples, sample => sample.Labels.Count == 1);
    }

    [Fact]
    public void Negativ_mit_Register_Split_Hinweis_uebernimmt_den_Split()
    {
        var source = CreateFile("frame.png", PngBytes);
        var forced = CreateNegative("neg_forced.bmp", BmpBytes(0x44)) with
        {
            SplitHint = TrainingExportTarget.Validation
        };

        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, [forced]);

        var negative = Assert.Single(bundle.Plan.Images, image => image.IsNegative);
        Assert.Equal(TrainingExportTarget.Validation, negative.Target);
    }

    // ── Hilfen ───────────────────────────────────────────────────────────

    private string CreateFile(string name, byte[] bytes)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private TrainingExportNegativeImage CreateNegative(string name, byte[] bytes)
    {
        var path = CreateFile(name, bytes);
        return new TrainingExportNegativeImage(
            path,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            SplitHint: null);
    }

    private TrainingExportPlanBundle CreateBundle(
        string sourcePath,
        TrainingExportHoldingRole role,
        IReadOnlyList<TrainingExportNegativeImage>? negatives = null,
        IReadOnlySet<string>? protectedImageHashes = null)
    {
        var classMap = new TrainingYoloClassMapSnapshot(
            YoloDetectClassMapV2.Version,
            new string('a', 64),
            YoloDetectClassMapV2.Classes,
            []);
        var registry = new TrainingExportRegistrySnapshot(
            TrainingExportRegistrySnapshot.CurrentSchemaVersion,
            new string('b', 64),
            TrainingExportRegistryApprovalStatus.Approved,
            "Test User",
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            new Dictionary<string, TrainingExportHoldingRole> { ["100-200"] = role },
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('c', 64))]);
        return new TrainingExportPlanService().CreatePlan(new TrainingExportPlanRequest(
            [new TrainingExportPlanCandidate(
                new TrainingExportSourceRef(TrainingExportSourceType.TrainingSample, "sample-1"),
                sourcePath,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath))),
                ".png",
                "100-200",
                "BAB_riss",
                TrainingYoloClassSourceKinds.ProductiveYoloName,
                new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1),
                TrainingInventoryDisposition.TrainValCandidate)],
            classMap,
            registry,
            "inventory-run",
            new Dictionary<string, string>
            {
                ["teacher_annotations.json"] = new string('d', 64),
                ["training_samples.json"] = new string('e', 64)
            },
            true,
            protectedImageHashes ?? new HashSet<string>(),
            new HashSet<string>(),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"))
        {
            NegativeImages = negatives ?? []
        });
    }

    private static byte[] BmpBytes(byte fill)
    {
        // Minimale, vom Format-Validator akzeptierte BMP-Datei (2x2 Pixel, Fuellbyte variiert
        // den Bild-Hash deterministisch).
        var bytes = new byte[64];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2), (uint)bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), 2);
        for (var index = 26; index < bytes.Length; index++)
            bytes[index] = fill;
        return bytes;
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
