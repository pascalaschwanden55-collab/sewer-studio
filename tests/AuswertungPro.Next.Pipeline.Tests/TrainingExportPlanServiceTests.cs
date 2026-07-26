using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingExportPlanServiceTests
{
    private static readonly DateTimeOffset GeneratedUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePlan_verwendet_ausschliesslich_freigegebene_Haltungsrollen()
    {
        var service = new TrainingExportPlanService();
        var candidates = new[]
        {
            Candidate("a-1", "100-200", "BAB_riss"),
            Candidate("b-1", "200-300", "BAC_bruch"),
            Candidate("a-2", "100-200", "BAC_bruch")
        };
        var registry = Registry(
            ("100-200", TrainingExportHoldingRole.DevelopmentValidation),
            ("200-300", TrainingExportHoldingRole.Train));

        var first = service.CreatePlan(Request(candidates, registry));
        var second = service.CreatePlan(Request(candidates.Reverse().ToArray(), registry));

        Assert.Equal(first.Plan.PlanId, second.Plan.PlanId);
        Assert.Equal(["200-300"], first.Plan.TrainHoldingKeys);
        Assert.Equal(["100-200"], first.Plan.ValidationHoldingKeys);
        Assert.All(
            first.Plan.Images.Where(image => image.HoldingKey == "100-200"),
            image => Assert.Equal(TrainingExportTarget.Validation, image.Target));
        Assert.All(
            first.Plan.Images.Where(image => image.HoldingKey == "200-300"),
            image => Assert.Equal(TrainingExportTarget.Train, image.Target));
        Assert.Empty(first.Plan.TrainHoldingKeys.Intersect(first.Plan.ValidationHoldingKeys));
    }

    [Fact]
    public void CreatePlan_blockiert_neue_Haltung_statt_den_Split_neu_zu_berechnen()
    {
        var service = new TrainingExportPlanService();

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            service.CreatePlan(Request(
                [Candidate("new", "900-901", "BAB_riss")],
                Registry(("100-200", TrainingExportHoldingRole.Train)))));

        Assert.Contains("keine freigegebene", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_uebernimmt_AP01_Quarantaene_und_Eval_Sperre_unveraendert()
    {
        var service = new TrainingExportPlanService();
        var bundle = service.CreatePlan(Request(
        [
            Candidate("origin", null, "BAB_riss", TrainingInventoryDisposition.QuarantineOrigin),
            Candidate("geometry", "100-200", "BAB_riss", TrainingInventoryDisposition.QuarantineGeometry),
            Candidate("eval", "200-300", "BAB_riss", TrainingInventoryDisposition.EvaluationLocked),
            Candidate("archive", null, "BAB_riss", TrainingInventoryDisposition.Archive),
            Candidate("clean", "300-400", "BAB_riss")
        ], Registry(("300-400", TrainingExportHoldingRole.Train))));

        Assert.Equal(TrainingExportExclusionReason.OriginQuarantine, Exclusion(bundle.Plan, "origin").Reason);
        Assert.Equal(TrainingExportExclusionReason.GeometryQuarantine, Exclusion(bundle.Plan, "geometry").Reason);
        Assert.Equal(TrainingExportExclusionReason.EvaluationLocked, Exclusion(bundle.Plan, "eval").Reason);
        Assert.Equal(TrainingExportExclusionReason.Archive, Exclusion(bundle.Plan, "archive").Reason);
        Assert.Single(bundle.Plan.Images);
        Assert.Equal("clean", Assert.Single(Assert.Single(bundle.Plan.Images).Labels).Sources.Single().SourceId);
    }

    [Fact]
    public void CreatePlan_blockiert_wenn_Eval_Schutz_nicht_vollstaendig_ist()
    {
        var service = new TrainingExportPlanService();

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            service.CreatePlan(Request(
                [Candidate("a", "100-200", "BAB_riss")],
                Registry(("100-200", TrainingExportHoldingRole.Train)),
                evaluationProtectionComplete: false)));

        Assert.Contains("Schutz", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_blockiert_nicht_menschlich_freigegebenes_Register()
    {
        var candidateRegistry = Registry(("100-200", TrainingExportHoldingRole.Train)) with
        {
            ApprovalStatus = TrainingExportRegistryApprovalStatus.Candidate,
            ApprovedBy = null,
            ApprovedUtc = null
        };

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportPlanService().CreatePlan(Request(
                [Candidate("a", "100-200", "BAB_riss")],
                candidateRegistry)));

        Assert.Contains("menschlich", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_bewahrt_feste_Klassen_IDs_Dateinamen_und_Manifestzahlen()
    {
        var service = new TrainingExportPlanService();
        var bundle = service.CreatePlan(Request(
        [
            Candidate("sample-a", "100-200", "BAB_riss", extension: ".JPG"),
            Candidate("sample-b", "200-300", "BAC_bruch", extension: "png")
        ], Registry(
            ("100-200", TrainingExportHoldingRole.Train),
            ("200-300", TrainingExportHoldingRole.DevelopmentValidation))));

        var crack = ImageForSource(bundle.Plan, "sample-a");
        var breakage = ImageForSource(bundle.Plan, "sample-b");
        Assert.Equal(1, Assert.Single(crack.Labels).ClassId);
        Assert.Equal("BAB_riss", Assert.Single(crack.Labels).ClassName);
        Assert.Equal($"img_{crack.ImageSha256}.jpg", crack.TargetFileName);
        Assert.Equal(2, Assert.Single(breakage.Labels).ClassId);
        Assert.Equal($"img_{breakage.ImageSha256}.png", breakage.TargetFileName);
        Assert.Equal(1, bundle.Plan.InstancesPerClass["BAB_riss"]);
        Assert.Equal(1, bundle.Plan.InstancesPerClass["BAC_bruch"]);
        Assert.Equal(YoloDetectClassMapV2.Version, bundle.Plan.ClassMapVersion);
        Assert.Equal(new string('a', 64), bundle.Plan.VsaManifestHash);
        Assert.DoesNotContain(@"C:\frames", System.Text.Json.JsonSerializer.Serialize(bundle.Plan));
    }

    [Fact]
    public void CreatePlan_rundet_Bildrand_Box_nach_innen()
    {
        var bundle = new TrainingExportPlanService().CreatePlan(Request(
            [
                Candidate(
                    "edge-box",
                    "100-200",
                    "BAB_riss",
                    box: new TrainingExportBoundingBox(
                        0.5615234375,
                        0.619140625,
                        0.802734375,
                        0.76171875))
            ],
            Registry(("100-200", TrainingExportHoldingRole.Train))));

        var box = Assert.Single(Assert.Single(bundle.Plan.Images).Labels).BoundingBox;

        Assert.True(box.IsValid);
        Assert.Equal(0.619141, box.YCenter);
        Assert.Equal(0.761718, box.Height);
        Assert.Equal(1, box.YCenter + box.Height / 2, precision: 6);
    }

    [Fact]
    public void CreatePlan_verwendet_SourceId_nie_als_Dateipfad()
    {
        var candidate = Candidate(@"..\..\kunde", "100-200", "BAB_riss");

        var bundle = new TrainingExportPlanService().CreatePlan(Request(
            [candidate],
            Registry(("100-200", TrainingExportHoldingRole.Train))));

        var image = Assert.Single(bundle.Plan.Images);
        Assert.Equal($"img_{candidate.ImageSha256}.jpg", image.TargetFileName);
        Assert.DoesNotContain("..", image.TargetFileName, StringComparison.Ordinal);
        Assert.DoesNotContain("kunde", image.TargetFileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_fuehrt_gleiches_Bild_und_identische_Labels_zusammen()
    {
        var hash = Sha("same-image");
        var service = new TrainingExportPlanService();
        var bundle = service.CreatePlan(Request(
        [
            Candidate("teacher", "100-200", "BAB_riss", imageSha256: hash,
                sourceType: TrainingExportSourceType.TeacherAnnotation),
            Candidate("sample", "100-200", "BAB_riss", imageSha256: hash)
        ], Registry(("100-200", TrainingExportHoldingRole.Train))));

        var image = Assert.Single(bundle.Plan.Images);
        var label = Assert.Single(image.Labels);
        Assert.Equal(2, label.Sources.Count);
        Assert.Contains(label.Sources, source => source.StableKey == "teacher:teacher");
        Assert.Contains(label.Sources, source => source.StableKey == "sample:sample");
        Assert.Single(bundle.SourcePathsByImageSha256);
    }

    [Fact]
    public void CreatePlan_sammelt_unterschiedliche_Labels_desselben_Bildes()
    {
        var hash = Sha("two-labels");
        var bundle = new TrainingExportPlanService().CreatePlan(Request(
        [
            Candidate("crack", "100-200", "BAB_riss", imageSha256: hash),
            Candidate(
                "break",
                "100-200",
                "BAC_bruch",
                imageSha256: hash,
                box: new TrainingExportBoundingBox(0.7, 0.7, 0.1, 0.1))
        ], Registry(("100-200", TrainingExportHoldingRole.Train))));

        Assert.Equal(2, Assert.Single(bundle.Plan.Images).Labels.Count);
    }

    [Fact]
    public void CreatePlan_blockiert_widerspruechliche_Haltung_beim_gleichen_Bild()
    {
        var hash = Sha("conflict");

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportPlanService().CreatePlan(Request(
            [
                Candidate("a", "100-200", "BAB_riss", imageSha256: hash),
                Candidate("b", "200-300", "BAC_bruch", imageSha256: hash)
            ], Registry(
                ("100-200", TrainingExportHoldingRole.Train),
                ("200-300", TrainingExportHoldingRole.DevelopmentValidation)))));

        Assert.Contains("widerspruechlichen Haltungen", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_schliesst_menschlich_freigegebenes_Discard_aus()
    {
        var classMap = Snapshot(
            new TrainingYoloClassMapping(
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                "BCD",
                null,
                TrainingYoloClassAction.Discard,
                null,
                TrainingYoloClassApprovalStatus.Approved));
        var bundle = new TrainingExportPlanService().CreatePlan(Request(
            [Candidate("scene", "100-200", "BCD")],
            Registry(("100-200", TrainingExportHoldingRole.Train)),
            classMap: classMap));

        Assert.Empty(bundle.Plan.Images);
        Assert.Equal(TrainingExportExclusionReason.ClassDiscarded, Exclusion(bundle.Plan, "scene").Reason);
    }

    [Fact]
    public void CreatePlan_sperrt_einen_zusaetzlich_geschuetzten_Bildhash()
    {
        var candidate = Candidate("protected", "100-200", "BAB_riss");
        var bundle = new TrainingExportPlanService().CreatePlan(Request(
            [candidate],
            Registry(("100-200", TrainingExportHoldingRole.Train)),
            protectedImageHashes: new HashSet<string> { candidate.ImageSha256! }));

        Assert.Empty(bundle.Plan.Images);
        Assert.Equal(TrainingExportExclusionReason.EvaluationLocked, Exclusion(bundle.Plan, "protected").Reason);
    }

    private static TrainingExportPlanRequest Request(
        IReadOnlyList<TrainingExportPlanCandidate> candidates,
        TrainingExportRegistrySnapshot registry,
        bool evaluationProtectionComplete = true,
        TrainingYoloClassMapSnapshot? classMap = null,
        IReadOnlySet<string>? protectedImageHashes = null)
        => new(
            Candidates: candidates,
            ClassMap: classMap ?? Snapshot(),
            Registry: registry,
            InventoryRunId: "inventory-run-fixed",
            SourceSnapshotHashes: new Dictionary<string, string>
            {
                ["teacher_annotations.json"] = Sha("teacher-source"),
                ["training_samples.json"] = Sha("sample-source")
            },
            EvaluationProtectionComplete: evaluationProtectionComplete,
            ProtectedImageHashes: protectedImageHashes ?? new HashSet<string>(),
            ProtectedHoldingKeys: new HashSet<string>(),
            GeneratedUtc: GeneratedUtc);

    private static TrainingExportPlanCandidate Candidate(
        string sourceId,
        string? holdingKey,
        string sourceClassKey,
        TrainingInventoryDisposition disposition = TrainingInventoryDisposition.TrainValCandidate,
        string extension = ".jpg",
        string? imageSha256 = null,
        TrainingExportSourceType sourceType = TrainingExportSourceType.TrainingSample,
        TrainingExportBoundingBox? box = null)
        => new(
            Source: new TrainingExportSourceRef(sourceType, sourceId),
            FramePath: $@"C:\frames\{sourceId}{extension}",
            ImageSha256: imageSha256 ?? Sha(sourceId),
            ImageExtension: extension,
            HoldingKey: holdingKey,
            SourceClassKey: sourceClassKey,
            ClassSourceKind: TrainingYoloClassSourceKinds.TeacherVsaCode,
            BoundingBox: box ?? new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1),
            InventoryDisposition: disposition);

    private static TrainingExportRegistrySnapshot Registry(
        params (string Holding, TrainingExportHoldingRole Role)[] assignments)
        => new(
            TrainingExportRegistrySnapshot.CurrentSchemaVersion,
            Sha("registry"),
            TrainingExportRegistryApprovalStatus.Approved,
            "Test User",
            GeneratedUtc,
            assignments.ToDictionary(item => item.Holding, item => item.Role),
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                Sha("dev-val-manifest"))]);

    private static TrainingExportExclusion Exclusion(TrainingExportPlan plan, string sourceId)
        => Assert.Single(plan.Exclusions, item => item.Source.SourceId == sourceId);

    private static TrainingExportPlannedImage ImageForSource(TrainingExportPlan plan, string sourceId)
        => Assert.Single(plan.Images, image =>
            image.Labels.SelectMany(label => label.Sources).Any(source => source.SourceId == sourceId));

    private static TrainingYoloClassMapSnapshot Snapshot(
        params TrainingYoloClassMapping[] mappings)
        => new(
            YoloDetectClassMapV2.Version,
            new string('a', 64),
            YoloDetectClassMapV2.Classes,
            mappings);

    private static string Sha(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
