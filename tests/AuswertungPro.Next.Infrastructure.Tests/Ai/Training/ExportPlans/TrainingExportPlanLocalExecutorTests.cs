using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportPlanLocalExecutorTests : IDisposable
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-local-executor-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_schreibt_den_Plan_byte_stabil_und_atomar()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.DevelopmentValidation);
        var datasetRoot = Path.Combine(_root, "datasets");

        var result = await new TrainingExportPlanLocalExecutor().ExecuteAsync(bundle, datasetRoot);

        Assert.Equal(TrainingExportExecutionStatus.Created, result.Status);
        Assert.Equal(bundle.Plan.PlanId, result.PlanId);
        var image = Assert.Single(bundle.Plan.Images);
        Assert.Equal(
            PngBytes,
            await File.ReadAllBytesAsync(Path.Combine(result.DatasetPath, "images", "val", image.TargetFileName)));
        Assert.Equal(
            "1 0.500000 0.500000 0.200000 0.100000\n",
            await File.ReadAllTextAsync(Path.Combine(
                result.DatasetPath,
                "labels",
                "val",
                $"{Path.GetFileNameWithoutExtension(image.TargetFileName)}.txt")));
        Assert.Equal(
            string.Join('\n', bundle.Plan.Classes) + "\n",
            await File.ReadAllTextAsync(Path.Combine(result.DatasetPath, "classes.txt")));
        Assert.StartsWith("path: .\ntrain: images/train\nval: images/val\n", await File.ReadAllTextAsync(result.DataYamlPath));
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(Path.Combine(result.DatasetPath, "_export_receipt.json")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(datasetRoot, ".staging")));
    }

    [Fact]
    public async Task ExecuteAsync_gleicher_fertiger_Plan_ist_idempotent()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);
        var executor = new TrainingExportPlanLocalExecutor();
        var datasetRoot = Path.Combine(_root, "datasets");
        var first = await executor.ExecuteAsync(bundle, datasetRoot);
        var label = Directory.EnumerateFiles(first.DatasetPath, "*.txt", SearchOption.AllDirectories)
            .Single(path => Path.GetDirectoryName(path)!.Contains("labels", StringComparison.OrdinalIgnoreCase));
        var before = File.GetLastWriteTimeUtc(label);

        var second = await executor.ExecuteAsync(bundle, datasetRoot);

        Assert.Equal(TrainingExportExecutionStatus.AlreadyComplete, second.Status);
        Assert.Equal(before, File.GetLastWriteTimeUtc(label));
    }

    [Fact]
    public async Task ExecuteAsync_repariert_keinen_beschaedigten_bestehenden_Datensatz()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);
        var executor = new TrainingExportPlanLocalExecutor();
        var datasetRoot = Path.Combine(_root, "datasets");
        var first = await executor.ExecuteAsync(bundle, datasetRoot);
        var label = Directory.EnumerateFiles(Path.Combine(first.DatasetPath, "labels"), "*.txt", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(label, "kaputt");

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            executor.ExecuteAsync(bundle, datasetRoot));

        Assert.Contains("Konflikt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("kaputt", await File.ReadAllTextAsync(label));
    }

    [Fact]
    public async Task ExecuteAsync_veraendertes_Original_hinterlaesst_keinen_Datensatz()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);
        await File.WriteAllBytesAsync(source, [9, 9, 9]);
        var datasetRoot = Path.Combine(_root, "datasets");

        await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportPlanLocalExecutor().ExecuteAsync(bundle, datasetRoot));

        Assert.False(Directory.Exists(Path.Combine(datasetRoot, bundle.Plan.PlanId)));
    }

    [Fact]
    public async Task ExecuteAsync_lehnt_ungueltige_Bildbytes_auch_bei_passendem_Hash_ab()
    {
        var source = CreateSource();
        await File.WriteAllBytesAsync(source, "kein bild"u8.ToArray());
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);
        var datasetRoot = Path.Combine(_root, "datasets");

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportPlanLocalExecutor().ExecuteAsync(bundle, datasetRoot));

        Assert.Contains("Bildformat", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(datasetRoot, bundle.Plan.PlanId)));
    }

    [Fact]
    public async Task ExecuteAsync_lehnt_eine_zum_Bild_unpassende_Endung_ab()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train, ".jpg");
        var datasetRoot = Path.Combine(_root, "datasets");

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            new TrainingExportPlanLocalExecutor().ExecuteAsync(bundle, datasetRoot));

        Assert.Contains("Dateiendung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(datasetRoot, bundle.Plan.PlanId)));
    }

    [Fact]
    public async Task ExecuteAsync_akzeptiert_keine_zusaetzlichen_Dateien_oder_Unterordner()
    {
        var source = CreateSource();
        var bundle = CreateBundle(source, TrainingExportHoldingRole.Train);
        var executor = new TrainingExportPlanLocalExecutor();
        var datasetRoot = Path.Combine(_root, "datasets");
        var first = await executor.ExecuteAsync(bundle, datasetRoot);
        var extraFile = Path.Combine(first.DatasetPath, "customer-note.txt");
        await File.WriteAllTextAsync(extraFile, "unveraendert");

        await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            executor.ExecuteAsync(bundle, datasetRoot));
        Assert.Equal("unveraendert", await File.ReadAllTextAsync(extraFile));

        File.Delete(extraFile);
        var extraDirectory = Path.Combine(first.DatasetPath, "images", "train", "unerwartet");
        Directory.CreateDirectory(extraDirectory);
        await Assert.ThrowsAsync<TrainingExportPlanException>(() =>
            executor.ExecuteAsync(bundle, datasetRoot));
        Assert.True(Directory.Exists(extraDirectory));
    }

    private string CreateSource()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "frame.png");
        File.WriteAllBytes(path, PngBytes);
        return path;
    }

    private static TrainingExportPlanBundle CreateBundle(
        string sourcePath,
        TrainingExportHoldingRole role,
        string imageExtension = ".png")
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
                imageExtension,
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

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
