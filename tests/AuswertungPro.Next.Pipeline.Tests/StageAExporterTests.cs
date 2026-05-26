using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
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
        Assert.Contains("'BABAC'", dataYaml);

        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(result.ManifestPath!))!.AsObject();
        Assert.Equal(5, manifest["input_samples"]!.GetValue<int>());
        Assert.Equal(1, manifest["skipped_eval_set"]!.GetValue<int>());
        Assert.Equal(1, manifest["final_samples"]!.GetValue<int>());
        Assert.Equal(result.EvalHashListSha256, manifest["eval_hash_list_sha256"]!.GetValue<string>());
    }

    [Fact]
    public async Task DryRun_require_bbox_filtert_samples_ohne_echte_box()
    {
        var source = PrepareSourceWithBboxAndNoBbox();
        var output = Path.Combine(_root, "bbox-out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: true,
            ValidationRatio: 0,
            DegreeOfParallelism: 2,
            RequireBoundingBox: true));

        Assert.Equal(2, result.InputSamples);
        Assert.Equal(1, result.SkippedWithoutBoundingBox);
        Assert.Equal(1, result.FinalSamples);
        Assert.Equal("BABAC", Assert.Single(result.Classes).ClassName);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task DryRun_sperrt_legacy_und_unbekannte_aufnahmedaten()
    {
        var source = PrepareSourceWithTrainingEligibility();
        var output = Path.Combine(_root, "eligibility-out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: true,
            ValidationRatio: 0,
            DegreeOfParallelism: 2));

        Assert.Equal(3, result.InputSamples);
        Assert.Equal(2, result.SkippedTrainingIneligible);
        Assert.Equal(1, result.FinalSamples);
        Assert.Equal("BCAEA", Assert.Single(result.Classes).ClassName);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task DryRun_entfernt_doppelte_Bilder_per_Hash()
    {
        var source = PrepareSourceWithDuplicateImages();
        var output = Path.Combine(_root, "dedupe-out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: true,
            ValidationRatio: 0,
            DegreeOfParallelism: 2,
            RequireBoundingBox: true));

        Assert.Equal(3, result.InputSamples);
        Assert.Equal(2, result.SkippedDuplicateImage);
        Assert.Equal(1, result.FinalSamples);
        Assert.Equal("BDDC", Assert.Single(result.Classes).ClassName);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Export_preserves_vsa_kek_codes_and_catalog_metadata_in_clean_samples()
    {
        var source = PrepareVsaKekCatalogSamples();
        var output = Path.Combine(_root, "vsa-kek-out");

        var result = await new StageAExporter().ExportAsync(new StageAExportOptions(
            SourceSamplesPath: source.SamplesPath,
            EvalSetRoot: source.EvalRoot,
            OutputRoot: output,
            DryRun: false,
            ValidationRatio: 0,
            DegreeOfParallelism: 2));

        Assert.Equal(3, result.FinalSamples);
        Assert.Equal(new[] { "BAGA", "BCCYY", "BDBA" }, result.Classes.Select(c => c.ClassName).OrderBy(c => c).ToArray());

        var clean = JsonSerializer.Deserialize<TrainingSample[]>(
            await File.ReadAllTextAsync(result.CleanTrainingSamplesPath!),
            StageAExporter.JsonOptions)!;

        Assert.Contains(clean, s =>
            s.Code == "BAGA"
            && s.CodeMeta?.Code == "BAGA"
            && s.CodeMeta.Parameters["catalog.canonicalCode"] == "BAG");
        Assert.Contains(clean, s =>
            s.Code == "BDBA"
            && s.CodeMeta?.Code == "BDBA"
            && s.CodeMeta.Parameters["catalog.standardAnnotation"] == "A");
        Assert.Contains(clean, s =>
            s.Code == "BCCYY"
            && s.CodeMeta?.Code == "BCCYY"
            && s.CodeMeta.Parameters["catalog.source"] == VsaKekCatalogSources.XtfObserved);
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
            MarkEligible(new TrainingSample { SampleId = "eval", Code = "BCAAA", FramePath = eval, Status = TrainingSampleStatus.Approved }),
            MarkEligible(new TrainingSample { SampleId = "missing", Code = "BAHC", FramePath = Path.Combine(frameRoot, "missing.png"), Status = TrainingSampleStatus.Approved }),
            MarkEligible(new TrainingSample { SampleId = "badcode", Code = "", FramePath = good, Status = TrainingSampleStatus.Approved }),
            MarkEligible(new TrainingSample { SampleId = "ok", Code = "BABAC", FramePath = good, Status = TrainingSampleStatus.Approved }),
        };

        var samplesPath = Path.Combine(_root, "training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private (string SamplesPath, string EvalRoot) PrepareSourceWithBboxAndNoBbox()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "bbox-frames");
        Directory.CreateDirectory(frameRoot);

        var withBox = Path.Combine(frameRoot, "with-box.png");
        var noBox = Path.Combine(frameRoot, "no-box.png");
        File.WriteAllBytes(withBox, [1, 1, 1, 1]);
        File.WriteAllBytes(noBox, [2, 2, 2, 2]);

        var evalRoot = Path.Combine(_root, "bbox-eval");
        Directory.CreateDirectory(Path.Combine(evalRoot, "images"));
        File.WriteAllText(
            Path.Combine(evalRoot, "_manifest.json"),
            new JsonObject
            {
                ["frozen"] = true,
                ["hash_algorithm"] = "sha256",
                ["hashes"] = new JsonObject()
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var samples = new[]
        {
            MarkEligible(new TrainingSample
            {
                SampleId = "with-box",
                Code = "BABAC",
                FramePath = withBox,
                Status = TrainingSampleStatus.Approved,
                BboxXCenter = 0.5,
                BboxYCenter = 0.5,
                BboxWidth = 0.2,
                BboxHeight = 0.3,
            }),
            MarkEligible(new TrainingSample
            {
                SampleId = "no-box",
                Code = "BCAAA",
                FramePath = noBox,
                Status = TrainingSampleStatus.Approved,
            }),
        };

        var samplesPath = Path.Combine(_root, "bbox_training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private (string SamplesPath, string EvalRoot) PrepareSourceWithDuplicateImages()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "dedupe-frames");
        Directory.CreateDirectory(frameRoot);

        var imageA = Path.Combine(frameRoot, "a.png");
        var imageB = Path.Combine(frameRoot, "b.png");
        var imageC = Path.Combine(frameRoot, "c.png");
        File.WriteAllBytes(imageA, [7, 7, 7, 7]);
        File.WriteAllBytes(imageB, [7, 7, 7, 7]);
        File.WriteAllBytes(imageC, [7, 7, 7, 7]);

        var evalRoot = Path.Combine(_root, "dedupe-eval");
        Directory.CreateDirectory(Path.Combine(evalRoot, "images"));
        File.WriteAllText(
            Path.Combine(evalRoot, "_manifest.json"),
            new JsonObject
            {
                ["frozen"] = true,
                ["hash_algorithm"] = "sha256",
                ["hashes"] = new JsonObject()
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var samples = new[]
        {
            MakeBddSample("first", imageA),
            MakeBddSample("duplicate-1", imageB),
            MakeBddSample("duplicate-2", imageC),
        };

        var samplesPath = Path.Combine(_root, "dedupe_training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private (string SamplesPath, string EvalRoot) PrepareSourceWithTrainingEligibility()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "eligibility-frames");
        Directory.CreateDirectory(frameRoot);

        var oldFrame = Path.Combine(frameRoot, "old.png");
        var unknownFrame = Path.Combine(frameRoot, "unknown.png");
        var currentFrame = Path.Combine(frameRoot, "current.png");
        File.WriteAllBytes(oldFrame, [1, 1, 1]);
        File.WriteAllBytes(unknownFrame, [2, 2, 2]);
        File.WriteAllBytes(currentFrame, [3, 3, 3]);

        var evalRoot = Path.Combine(_root, "eligibility-eval");
        Directory.CreateDirectory(Path.Combine(evalRoot, "images"));
        File.WriteAllText(
            Path.Combine(evalRoot, "_manifest.json"),
            new JsonObject
            {
                ["frozen"] = true,
                ["hash_algorithm"] = "sha256",
                ["hashes"] = new JsonObject()
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var samples = new[]
        {
            new TrainingSample
            {
                SampleId = "legacy",
                Code = "BCAEA",
                FramePath = oldFrame,
                Status = TrainingSampleStatus.Approved,
                InspectionDate = new DateTime(2021, 12, 31),
                TrainingEligible = false,
                TrainingEligibilityReason = TrainingSampleEligibility.LegacyBeforeCutoffReason,
            },
            new TrainingSample
            {
                SampleId = "unknown",
                Code = "BCAEA",
                FramePath = unknownFrame,
                Status = TrainingSampleStatus.Approved,
                TrainingEligible = false,
                TrainingEligibilityReason = TrainingSampleEligibility.MissingInspectionDateReason,
            },
            new TrainingSample
            {
                SampleId = "current",
                Code = "BCAEA",
                FramePath = currentFrame,
                Status = TrainingSampleStatus.Approved,
                InspectionDate = new DateTime(2022, 1, 1),
                TrainingEligible = true,
            },
        };

        var samplesPath = Path.Combine(_root, "eligibility_training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private (string SamplesPath, string EvalRoot) PrepareVsaKekCatalogSamples()
    {
        Directory.CreateDirectory(_root);
        var frameRoot = Path.Combine(_root, "vsa-kek-frames");
        Directory.CreateDirectory(frameRoot);

        var imageBaga = Path.Combine(frameRoot, "baga.png");
        var imageBdba = Path.Combine(frameRoot, "bdba.png");
        var imageBccyy = Path.Combine(frameRoot, "bccyy.png");
        File.WriteAllBytes(imageBaga, [1, 2, 3]);
        File.WriteAllBytes(imageBdba, [4, 5, 6]);
        File.WriteAllBytes(imageBccyy, [7, 8, 9]);

        var evalRoot = Path.Combine(_root, "vsa-kek-eval");
        Directory.CreateDirectory(Path.Combine(evalRoot, "images"));
        File.WriteAllText(
            Path.Combine(evalRoot, "_manifest.json"),
            new JsonObject
            {
                ["frozen"] = true,
                ["hash_algorithm"] = "sha256",
                ["hashes"] = new JsonObject()
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var samples = new[]
        {
            MakeVsaKekSample("baga", "BAGA", imageBaga, VsaKekCatalogSources.Ili, "BAG", null),
            MakeVsaKekSample("bdba", "BDBA", imageBdba, VsaKekCatalogSources.Ili, "BDB", "A"),
            MakeVsaKekSample("bccyy", "BCCYY", imageBccyy, VsaKekCatalogSources.XtfObserved, "BCCYY", null),
        };

        var samplesPath = Path.Combine(_root, "vsa_kek_training_samples.json");
        File.WriteAllText(samplesPath, JsonSerializer.Serialize(samples, StageAExporter.JsonOptions));
        return (samplesPath, evalRoot);
    }

    private static TrainingSample MakeBddSample(string sampleId, string framePath)
        => new()
        {
            SampleId = sampleId,
            Code = "BDDC",
            FramePath = framePath,
            Status = TrainingSampleStatus.Approved,
            InspectionDate = new DateTime(2022, 1, 1),
            TrainingEligible = true,
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.3,
        };

    private static TrainingSample MakeVsaKekSample(
        string sampleId,
        string code,
        string framePath,
        string source,
        string canonicalCode,
        string? standardAnnotation)
        => new()
        {
            SampleId = sampleId,
            Code = code,
            FramePath = framePath,
            Status = TrainingSampleStatus.Approved,
            InspectionDate = new DateTime(2022, 1, 1),
            TrainingEligible = true,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = code,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["catalog.source"] = source,
                    ["catalog.canonicalCode"] = canonicalCode,
                    ["catalog.standardAnnotation"] = standardAnnotation ?? string.Empty
                }
            },
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.3,
        };

    private static TrainingSample MarkEligible(TrainingSample sample)
    {
        sample.InspectionDate = new DateTime(2022, 1, 1);
        sample.TrainingEligible = true;
        return sample;
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
