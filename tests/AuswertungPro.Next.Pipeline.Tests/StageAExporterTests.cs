using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class StageAExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-stagea-" + Guid.NewGuid().ToString("N"));

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
    public async Task DryRun_filtert_eval_missing_ungueltig_und_schreibt_nichts()
    {
        var source = PrepareSourceWithEvalOverlap();
        var output = Path.Combine(_root, "out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: true,
            ValidationRatio: 0.25,
            DegreeOfParallelism: 2));

        Assert.True(result.DryRun);
        Assert.Equal(5, result.InputSamples);
        Assert.Equal(4, result.ApprovedSamples);
        Assert.Equal(1, result.SkippedNotApproved);
        Assert.Equal(1, result.SkippedEvalSet);
        Assert.Equal(1, result.SkippedMissingOrCorrupt);
        Assert.Equal(1, result.SkippedInvalidCode);
        Assert.Equal(1, result.FinalSamples);
        Assert.False(Directory.Exists(output));
        Assert.Equal(64, result.EvalHashListSha256.Length);
    }

    [Fact]
    public async Task Export_schreibt_clean_samples_manifest_data_yaml_und_labels()
    {
        var source = PrepareSourceWithEvalOverlap();
        var output = Path.Combine(_root, "out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: false,
            ValidationRatio: 0,
            DegreeOfParallelism: 2));

        Assert.False(result.DryRun);
        Assert.Equal(1, result.FinalSamples);
        Assert.Equal(1, result.TrainSamples);
        Assert.Equal(0, result.ValidationSamples);
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.CleanTrainingSamplesPath));
        Assert.True(File.Exists(result.DataYamlPath));

        var clean = JsonSerializer.Deserialize<TrainingSample[]>(
            await File.ReadAllTextAsync(result.CleanTrainingSamplesPath!),
            StageAExporter.JsonOptions)!;

        Assert.Single(clean);
        Assert.Equal("BABAC", clean[0].Code);
        Assert.StartsWith(output, clean[0].FramePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(clean[0].FramePath));

        var labelPath = Directory
            .EnumerateFiles(Path.Combine(output, "labels", "train"), "*.txt")
            .Single();
        Assert.Equal("0 0.500000 0.500000 0.800000 0.800000", File.ReadAllText(labelPath).Trim());

        var dataYaml = await File.ReadAllTextAsync(result.DataYamlPath!);
        Assert.Contains("nc: 1", dataYaml);
        Assert.Contains("'BAB'", dataYaml);

        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(result.ManifestPath!))!.AsObject();
        Assert.Equal(5, manifest["input_samples"]!.GetValue<int>());
        Assert.Equal(1, manifest["skipped_eval_set"]!.GetValue<int>());
        Assert.Equal(1, manifest["final_samples"]!.GetValue<int>());
        Assert.Equal(result.EvalHashListSha256, manifest["eval_hash_list_sha256"]!.GetValue<string>());
    }

    private (string SamplesPath, string EvalRoot) PrepareSourceWithEvalOverlap()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "frames");
        Directory.CreateDirectory(frameRoot);

        var good = Path.Combine(frameRoot, "good.png");
        var eval = Path.Combine(frameRoot, "eval.png");
        File.WriteAllBytes(good, [1, 2, 3, 4]);
        File.WriteAllBytes(eval, [9, 8, 7, 6]);

        var evalRoot = Path.Combine(_root, "eval");
        Directory.CreateDirectory(Path.Combine(evalRoot, "images"));
        var evalImage = Path.Combine(evalRoot, "images", "eval.png");
        File.Copy(eval, evalImage);
        WriteEvalManifest(evalRoot, evalImage);

        var samples = new[]
        {
            new TrainingSample { SampleId = "rejected", Code = "BCE", FramePath = good, Status = TrainingSampleStatus.Rejected },
            new TrainingSample { SampleId = "eval", Code = "BCAAA", FramePath = eval, Status = TrainingSampleStatus.Approved },
            new TrainingSample { SampleId = "missing", Code = "BAHC", FramePath = Path.Combine(frameRoot, "missing.png"), Status = TrainingSampleStatus.Approved },
            new TrainingSample { SampleId = "badcode", Code = "", FramePath = good, Status = TrainingSampleStatus.Approved },
            new TrainingSample { SampleId = "ok", Code = "BABAC", FramePath = good, Status = TrainingSampleStatus.Approved },
        };

        var samplesPath = Path.Combine(_root, "training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private static void WriteEvalManifest(string evalRoot, string evalImage)
    {
        var rel = "images/" + Path.GetFileName(evalImage);
        var manifest = new JsonObject
        {
            ["frozen"] = true,
            ["hash_algorithm"] = "sha256",
            ["hashes_count"] = 1,
            ["hashes"] = new JsonObject
            {
                [rel] = new JsonObject
                {
                    ["sha256"] = Sha256Hex(evalImage),
                    ["size_bytes"] = new FileInfo(evalImage).Length,
                }
            }
        };

        File.WriteAllText(
            Path.Combine(evalRoot, "_manifest.json"),
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Sha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
