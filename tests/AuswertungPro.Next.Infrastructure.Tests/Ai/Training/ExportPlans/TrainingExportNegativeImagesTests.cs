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

    [Fact]
    public void Streng_gebundenes_Negativ_traegt_echte_Haltung_und_Split_im_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var bound = BindNegative(
            CreateNegative("neg_bound.bmp", BmpBytes(0x45)),
            "638910-1367",
            TrainingExportTarget.Validation);

        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, [bound]);

        var negative = Assert.Single(bundle.Plan.Images, image => image.IsNegative);
        Assert.Equal("638910-1367", negative.HoldingKey);
        Assert.Equal(TrainingExportTarget.Validation, negative.Target);
        Assert.Contains("638910-1367", bundle.Plan.ValidationHoldingKeys);
        Assert.DoesNotContain(TrainingExportNegativePool.HoldingKey, bundle.Plan.ValidationHoldingKeys);
    }

    [Fact]
    public void Gebundene_Gegenrichtungen_mit_widerspruechlichem_Split_stoppen()
    {
        var source = CreateFile("frame.png", PngBytes);
        var train = BindNegative(
            CreateNegative("neg_train.bmp", BmpBytes(0x46)),
            "100-200",
            TrainingExportTarget.Train);
        var validation = BindNegative(
            CreateNegative("neg_val.bmp", BmpBytes(0x47)),
            "200-100",
            TrainingExportTarget.Validation);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(source, TrainingExportHoldingRole.Train, [train, validation]));

        Assert.Contains("Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Train", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gebundenes_Negativ_mit_beliebigem_Haltungstext_stoppt_den_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var bound = BindNegative(
            CreateNegative("neg_invalid_holding.bmp", BmpBytes(0x4A)),
            "keine-haltung",
            TrainingExportTarget.Train);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(source, TrainingExportHoldingRole.Train, [bound]));

        Assert.Contains("Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Schachtpaar", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gebundene_Negativ_Haltung_in_geschuetzter_Gegenrichtung_stoppt_den_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var bound = BindNegative(
            CreateNegative("neg_eval.bmp", BmpBytes(0x48)),
            "200-100",
            TrainingExportTarget.Train);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(
                source,
                TrainingExportHoldingRole.Train,
                [bound],
                protectedHoldingKeys: new HashSet<string> { "100-200" }));

        Assert.Contains("Eval", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gebundenes_Negativ_mit_falscher_aktiver_Klassenkarte_stoppt_den_Plan()
    {
        var source = CreateFile("frame.png", PngBytes);
        var bound = BindNegative(
            CreateNegative("neg_wrong_class_map.bmp", BmpBytes(0x4B)),
            "100-200",
            TrainingExportTarget.Train);
        var wrongClassMap = new TrainingYoloClassMapSnapshot(
            YoloDetectClassMapV3.Version,
            new string('d', 64),
            YoloDetectClassMapV3.Classes,
            [],
            classMapSha256: new string('8', 64));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            CreateBundle(
                source,
                TrainingExportHoldingRole.Train,
                [bound],
                classMap: wrongClassMap));

        Assert.Contains("Klassenkarte", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InputBuilder_blockiert_gebundene_Negativ_Haltung_in_geschuetzter_Gegenrichtung()
    {
        var bound = BindNegative(
            CreateNegative("neg_input_eval.bmp", BmpBytes(0x49)),
            "200-100",
            TrainingExportTarget.Train);
        var registry = CreateRegistry(
            TrainingExportHoldingRole.Train,
            [bound]);
        var inventory = CreateInventorySnapshot(
            protectedHoldingKeys: new HashSet<string> { "100-200" });
        var classMap = CreateActiveClassMap();

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportPlanInputBuilder().BuildAsync(
                inventory,
                registry,
                new HashSet<string>(),
                classMap,
                DateTimeOffset.Parse("2026-07-17T08:00:00Z")));

        Assert.Contains("Eval", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InputBuilder_blockiert_striktes_Negativ_mit_falschem_Klassenkarten_Hash()
    {
        var bound = BindNegative(
            CreateNegative("neg_input_class_map.bmp", BmpBytes(0x4C)),
            "300-400",
            TrainingExportTarget.Train);
        var registry = CreateRegistry(
            TrainingExportHoldingRole.Train,
            [bound]);
        var inventory = CreateInventorySnapshot(new HashSet<string> { "900-901" });
        var wrongClassMap = new TrainingYoloClassMapSnapshot(
            YoloDetectClassMapV3.Version,
            new string('d', 64),
            YoloDetectClassMapV3.Classes,
            [],
            classMapSha256: new string('8', 64));

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportPlanInputBuilder().BuildAsync(
                inventory,
                registry,
                new HashSet<string>(),
                wrongClassMap,
                DateTimeOffset.Parse("2026-07-17T08:00:00Z")));

        Assert.Contains("Klassenkarte", error.Message, StringComparison.OrdinalIgnoreCase);
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

    private static TrainingExportNegativeImage BindNegative(
        TrainingExportNegativeImage negative,
        string holdingKey,
        TrainingExportTarget split)
    {
        var parts = holdingKey.Split('-', StringSplitOptions.None);
        var physicalHoldingKey = parts.Length == 2
            ? StringComparer.Ordinal.Compare(parts[0], parts[1]) <= 0
                ? $"{parts[0]}|{parts[1]}"
                : $"{parts[1]}|{parts[0]}"
            : holdingKey;
        return negative with
        {
            NegativeSourceType = "reviewed_negative_set",
            HoldingKey = holdingKey,
            PhysicalHoldingKey = physicalHoldingKey,
            SplitHint = split,
            NegativeSetId = new string('a', 64),
            NegativeSetManifestSha256 = new string('6', 64),
            QueueId = new string('b', 64),
            ReviewSha256 = new string('7', 64),
            QueueManifestSha256 = new string('8', 64),
            CandidatesSha256 = new string('c', 64),
            ClassMapVersion = 3,
            ClassMapSha256 = new string('9', 64),
            VsaManifestHash = new string('d', 64),
            ReviewItemId = "bcc-hn-review-item",
            ReviewDecision = "all_classes_clear"
        };
    }

    private TrainingExportPlanBundle CreateBundle(
        string sourcePath,
        TrainingExportHoldingRole role,
        IReadOnlyList<TrainingExportNegativeImage>? negatives = null,
        IReadOnlySet<string>? protectedImageHashes = null,
        IReadOnlySet<string>? protectedHoldingKeys = null,
        TrainingYoloClassMapSnapshot? classMap = null)
    {
        classMap ??= CreateActiveClassMap();
        var registry = CreateRegistry(role, negatives);
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
            protectedHoldingKeys ?? new HashSet<string>(),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"))
        {
            NegativeImages = negatives ?? []
        });
    }

    private static TrainingYoloClassMapSnapshot CreateActiveClassMap()
        => new(
            YoloDetectClassMapV3.Version,
            new string('d', 64),
            YoloDetectClassMapV3.Classes,
            [],
            classMapSha256: new string('9', 64));

    private static TrainingExportRegistrySnapshot CreateRegistry(
        TrainingExportHoldingRole role,
        IReadOnlyList<TrainingExportNegativeImage>? negatives = null)
        => new(
            TrainingExportRegistrySnapshot.CurrentSchemaVersion,
            new string('b', 64),
            TrainingExportRegistryApprovalStatus.Approved,
            "Test User",
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            new Dictionary<string, TrainingExportHoldingRole> { ["100-200"] = role },
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('c', 64))])
        {
            NegativeImages = negatives ?? []
        };

    private static TrainingDataInventoryRuntimeSnapshot CreateInventorySnapshot(
        IReadOnlySet<string> protectedHoldingKeys)
    {
        var setRoot = Path.Combine(Path.GetTempPath(), "training-export-negative-tests", "eval");
        var knowledgeRoot = Path.Combine(
            Path.GetTempPath(),
            "training-export-negative-tests",
            "knowledge");
        var sources = new[]
        {
            CreateInventorySource(
                Path.Combine(knowledgeRoot, "teacher_annotations.json"),
                TrainingInventoryDataKind.TeacherAnnotations,
                new string('a', 64)),
            CreateInventorySource(
                Path.Combine(knowledgeRoot, "training_samples.json"),
                TrainingInventoryDataKind.TrainingSamples,
                new string('b', 64))
        };
        var status = new TrainingInventoryEvalProtectionStatus
        {
            ImageHashCheckEnabled = true,
            Sets =
            [
                new TrainingInventoryEvalSetStatus
                {
                    RootPath = setRoot,
                    ImageFiles = 1,
                    ManifestImageHashes = 1,
                    VerifiedImageHashes = 1,
                    HoldingKeys = protectedHoldingKeys.Count,
                    ImageHashesComplete = true,
                    HoldingKeysComplete = true
                }
            ]
        };
        return new TrainingDataInventoryRuntimeSnapshot(
            new TrainingDataInventoryReport
            {
                KnowledgeRoot = knowledgeRoot,
                RunId = "11111111111111111111111111111111",
                GeneratedUtc = DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
                EvalSetRoot = setRoot,
                SearchRoots = [knowledgeRoot],
                ProtectedRoots = [setRoot],
                EvalProtection = status,
                Sources = sources,
                Summary = TrainingInventorySummaryBuilder.Build([], sources)
            },
            [],
            [],
            new TrainingInventoryProtectionSnapshot(
                status,
                new HashSet<string>(),
                protectedHoldingKeys,
                [new TrainingInventoryProtectedSetSnapshot(
                    "dev-val-v1",
                    setRoot,
                    new string('c', 64))],
                new string('f', 64)));
    }

    private static TrainingInventorySourceDocument CreateInventorySource(
        string path,
        TrainingInventoryDataKind dataKind,
        string sha256)
        => new()
        {
            Path = path,
            DataKind = dataKind,
            Role = TrainingInventorySourceRole.Current,
            Bytes = 2,
            LastWriteUtc = DateTimeOffset.Parse("2026-07-17T07:00:00Z"),
            Sha256 = sha256,
            ParseState = TrainingInventoryParseState.Parsed,
            ValidationLevel = TrainingInventoryValidationLevel.TypedRecords,
            RecordCount = 0
        };

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
