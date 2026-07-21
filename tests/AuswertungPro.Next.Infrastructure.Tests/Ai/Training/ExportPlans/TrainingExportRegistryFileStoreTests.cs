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

    private TestPaths CreateFiles(
        string approvalStatus = "approved",
        string? approvedBy = "Test User",
        string? approvedUtc = "2026-07-17T08:00:00Z")
    {
        Directory.CreateDirectory(_root);
        var setRoot = Directory.CreateDirectory(Path.Combine(_root, "eval_set")).FullName;
        var manifestPath = Path.Combine(setRoot, "_manifest.json");
        File.WriteAllText(manifestPath, "{\"frozen\":true}");
        var manifestHash = Hash(manifestPath);
        var registryPath = Path.Combine(_root, "export_registry_v1.json");
        var approvedByJson = approvedBy is null ? "null" : $"\"{approvedBy}\"";
        var approvedUtcJson = approvedUtc is null ? "null" : $"\"{approvedUtc}\"";
        File.WriteAllText(
            registryPath,
            $$"""
              {
                "schema_version": "1.0",
                "approval_status": "{{approvalStatus}}",
                "approved_by": {{approvedByJson}},
                "approved_utc": {{approvedUtcJson}},
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
