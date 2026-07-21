using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportSidecarRequestBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-sidecar-builder-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildAsync_verpackt_exakt_den_Plan_ohne_neue_Entscheidung()
    {
        var path = CreateImage([1, 2, 3, 4]);
        var bundle = CreateBundle(path, TrainingExportHoldingRole.DevelopmentValidation);

        var request = await new TrainingExportSidecarRequestBuilder().BuildAsync(bundle);

        Assert.Equal("2.0", request.SchemaVersion);
        Assert.Equal(bundle.Plan.PlanId, request.PlanId);
        Assert.Equal(bundle.Plan.PlanId, request.PlanSha256);
        Assert.Equal(YoloDetectClassMapV2.Version, request.ClassMapVersion);
        var sample = Assert.Single(request.Samples);
        Assert.Equal("val", sample.Split);
        Assert.Equal(bundle.Plan.Images[0].TargetFileName, sample.TargetFileName);
        Assert.Equal([1, 2, 3, 4], Convert.FromBase64String(sample.ImageBase64));
        Assert.Equal(1, Assert.Single(sample.Labels).ClassId);
        var manifest = Encoding.UTF8.GetString(Convert.FromBase64String(request.ManifestJsonBase64));
        Assert.Contains("\"plan_id\"", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_blockiert_nachtraeglich_veraendertes_Originalbild()
    {
        var path = CreateImage([1, 2, 3, 4]);
        var bundle = CreateBundle(path, TrainingExportHoldingRole.Train);
        await File.WriteAllBytesAsync(path, [9, 9, 9]);

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportSidecarRequestBuilder().BuildAsync(bundle));

        Assert.Contains("veraendert", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private TrainingExportPlanBundle CreateBundle(
        string path,
        TrainingExportHoldingRole role)
    {
        var hash = Hash(File.ReadAllBytes(path));
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
                path,
                hash,
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
            new HashSet<string>(),
            new HashSet<string>(),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z")));
    }

    private string CreateImage(byte[] bytes)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "frame.png");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
