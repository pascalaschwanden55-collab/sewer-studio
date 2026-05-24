using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Ai.Evaluation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalSetManifestHasherTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-eval-hasher-" + Guid.NewGuid().ToString("N"));

    public EvalSetManifestHasherTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        Directory.CreateDirectory(Path.Combine(_root, "labels"));
        File.WriteAllText(Path.Combine(_root, "_manifest.json"), """{"frozen":true,"approved":1}""", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "_candidates.json"), """[{"id":"a"}]""", Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(_root, "images", "a.png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(_root, "labels", "a.txt"), "BAB", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "metrics_old.csv"), "changes after benchmark", Encoding.UTF8);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best effort test cleanup
        }
    }

    [Fact]
    public void ComputeHashes_includes_stable_eval_artifacts_only()
    {
        var result = EvalSetManifestHasher.ComputeHashes(_root);

        Assert.Equal("sha256", result.Algorithm);
        Assert.Equal(3, result.HashesCount);
        Assert.Contains(result.Hashes, h => h.RelativePath == "_candidates.json");
        Assert.Contains(result.Hashes, h => h.RelativePath == "images/a.png");
        Assert.Contains(result.Hashes, h => h.RelativePath == "labels/a.txt");
        Assert.DoesNotContain(result.Hashes, h => h.RelativePath == "_manifest.json");
        Assert.DoesNotContain(result.Hashes, h => h.RelativePath == "metrics_old.csv");
        Assert.All(result.Hashes, h => Assert.Equal(64, h.Sha256Hex.Length));
    }

    [Fact]
    public void ComputeAndStoreHashes_writes_hash_block_to_manifest()
    {
        var result = EvalSetManifestHasher.ComputeAndStoreHashes(_root);

        Assert.Equal(3, result.HashesCount);

        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "_manifest.json"), Encoding.UTF8))!.AsObject();
        Assert.True(manifest["frozen"]!.GetValue<bool>());
        Assert.Equal("sha256", manifest["hash_algorithm"]!.GetValue<string>());
        Assert.Equal(3, manifest["hashes_count"]!.GetValue<int>());
        Assert.NotNull(manifest["hashes_generated_utc"]);

        var hashes = manifest["hashes"]!.AsObject();
        Assert.Equal(3, hashes.Count);
        Assert.NotNull(hashes["_candidates.json"]);
        Assert.NotNull(hashes["images/a.png"]);
        Assert.NotNull(hashes["labels/a.txt"]);
    }
}
