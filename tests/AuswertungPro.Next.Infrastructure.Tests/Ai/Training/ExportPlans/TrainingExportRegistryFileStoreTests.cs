using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportRegistryFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-registry-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadBundle_liefert_freigegebene_Rollen_und_gepruefte_Schutzpfade()
    {
        var paths = CreateFiles();

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(TrainingExportRegistryApprovalStatus.Approved, bundle.Snapshot.ApprovalStatus);
        Assert.Equal(TrainingExportHoldingRole.Train, bundle.Snapshot.HoldingRoles["100-200"]);
        Assert.Equal(
            TrainingExportHoldingRole.DevelopmentValidation,
            bundle.Snapshot.HoldingRoles["200-300"]);
        Assert.Equal(["sample-a", "sample-b"], bundle.Snapshot.ApprovedSampleIds.Order());
        Assert.Equal(64, bundle.Snapshot.RegistryHash.Length);
        var protectedSet = Assert.Single(bundle.Snapshot.ProtectedSets);
        Assert.Equal("dev-val-v1", protectedSet.SetId);
        Assert.Equal(paths.SetRoot, bundle.ProtectedSetRootPaths["dev-val-v1"]);
    }

    [Fact]
    public void ReadBundle_blockiert_unbekannte_JSON_Felder()
    {
        var paths = CreateFiles();
        File.AppendAllText(paths.RegistryPath, "\n");
        var text = File.ReadAllText(paths.RegistryPath)
            .Replace("\"schema_version\"", "\"unknown\": true, \"schema_version\"", StringComparison.Ordinal);
        File.WriteAllText(paths.RegistryPath, text);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("sicher gelesen", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_geaendertes_Schutzmanifest()
    {
        var paths = CreateFiles();
        File.AppendAllText(paths.ManifestPath, "geaendert");

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Manifest-Hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_liefert_Kandidaten_ohne_sie_stillschweigend_freizugeben()
    {
        var paths = CreateFiles(approvalStatus: "candidate", approvedBy: null, approvedUtc: null);

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(TrainingExportRegistryApprovalStatus.Candidate, bundle.Snapshot.ApprovalStatus);
        Assert.Null(bundle.Snapshot.ApprovedBy);
        Assert.Null(bundle.Snapshot.ApprovedUtc);
    }

    [Fact]
    public void ReadBundle_blockiert_doppelte_Pilot_Sample_IDs()
    {
        var paths = CreateFiles(approvedSampleIdsJson: "[\"sample-a\", \"SAMPLE-A\"]");

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("mehrfach", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_liest_kuratierte_Negativbilder_mit_und_ohne_Split_Hinweis()
    {
        var shaA = new string('1', 64);
        var shaB = new string('2', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              { "path": "training/negatives/normal_01.png", "sha256": "{{shaA}}" },
              { "path": "training/negatives/normal_02.png", "sha256": "{{shaB}}", "split": "validation" }
            ]
            """);

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(2, bundle.Snapshot.NegativeImages.Count);
        var first = bundle.Snapshot.NegativeImages[0];
        Assert.Equal(shaA, first.Sha256);
        Assert.Null(first.SplitHint);
        Assert.True(Path.IsPathFullyQualified(first.Path));   // relativ -> KnowledgeRoot aufgeloest
        var second = bundle.Snapshot.NegativeImages[1];
        Assert.Equal(shaB, second.Sha256);
        Assert.Equal(TrainingExportTarget.Validation, second.SplitHint);
    }

    [Fact]
    public void ReadBundle_ohne_Negativfeld_bleibt_abwaertskompatibel()
    {
        var paths = CreateFiles();

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Empty(bundle.Snapshot.NegativeImages);
    }

    [Fact]
    public void ReadBundle_blockiert_Negativbild_ohne_Hash()
    {
        var paths = CreateFiles(negativesJson: """
            [ { "path": "training/negatives/normal_01.png" } ]
            """);

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_doppelte_Negativ_Hashes()
    {
        var sha = new string('1', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              { "path": "training/negatives/a.png", "sha256": "{{sha}}" },
              { "path": "training/negatives/b.png", "sha256": "{{sha}}" }
            ]
            """);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("mehrfach", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private TestPaths CreateFiles(
        string approvalStatus = "approved",
        string? approvedBy = "Test User",
        string? approvedUtc = "2026-07-17T08:00:00Z",
        string approvedSampleIdsJson = "[\"sample-a\", \"sample-b\"]",
        string? negativesJson = null)
    {
        Directory.CreateDirectory(_root);
        var setRoot = Directory.CreateDirectory(Path.Combine(_root, "eval_set")).FullName;
        var manifestPath = Path.Combine(setRoot, "_manifest.json");
        File.WriteAllText(manifestPath, "{\"frozen\":true}");
        var manifestHash = Hash(manifestPath);
        var registryPath = Path.Combine(_root, "export_registry_v1.json");
        var approvedByJson = approvedBy is null ? "null" : $"\"{approvedBy}\"";
        var approvedUtcJson = approvedUtc is null ? "null" : $"\"{approvedUtc}\"";
        var negativesSection = negativesJson is null ? string.Empty : $"\"negative_images\": {negativesJson},";
        File.WriteAllText(
            registryPath,
            $$"""
              {
                "schema_version": "1.0",
                "approval_status": "{{approvalStatus}}",
                "approved_by": {{approvedByJson}},
                "approved_utc": {{approvedUtcJson}},
                "approved_sample_ids": {{approvedSampleIdsJson}},
                {{negativesSection}}
                "holding_roles": {
                  "100-200": "train",
                  "200-300": "development_validation"
                },
                "protected_sets": [
                  {
                    "set_id": "dev-val-v1",
                    "role": "development_validation",
                    "root_path": "eval_set",
                    "manifest_sha256": "{{manifestHash}}"
                  }
                ]
              }
              """);
        return new TestPaths(registryPath, setRoot, manifestPath);
    }

    private static string Hash(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed record TestPaths(string RegistryPath, string SetRoot, string ManifestPath);
}
