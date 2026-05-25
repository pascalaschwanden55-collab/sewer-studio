using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Application.Ai.Training;

public sealed record StageAExportOptions(
    string SourceSamplesPath,
    string EvalSetRoot,
    string OutputRoot,
    bool DryRun,
    double ValidationRatio = 0.2,
    int DegreeOfParallelism = 0,
    bool RequireBoundingBox = false);

public sealed record StageAExportResult(
    bool DryRun,
    int InputSamples,
    int ApprovedSamples,
    int SkippedNotApproved,
    int SkippedEvalSet,
    int SkippedMissingOrCorrupt,
    int SkippedInvalidCode,
    int SkippedWithoutBoundingBox,
    int SkippedDuplicateImage,
    int FinalSamples,
    int TrainSamples,
    int ValidationSamples,
    int EvalHashesCount,
    string EvalHashListSha256,
    string? ManifestPath,
    string? CleanTrainingSamplesPath,
    string? DataYamlPath,
    IReadOnlyList<StageAExportClassCount> Classes);

public sealed record StageAExportClassCount(
    string ClassName,
    int Total,
    int Train,
    int Validation);

/// <summary>
/// Baut einen sauberen Stage-A-Trainingsdatensatz aus training_samples.json.
/// Eval-Bilder werden per SHA-256 ausgeschlossen, nicht per Dateiname.
/// </summary>
public sealed class StageAExporter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp",
    };

    public async Task<StageAExportResult> ExportAsync(
        StageAExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var samples = await LoadSamplesAsync(options.SourceSamplesPath, cancellationToken)
            .ConfigureAwait(false);
        var evalHashes = LoadEvalImageHashes(options.EvalSetRoot);
        var evalHashListSha256 = ComputeHashListSha256(evalHashes);

        var analyses = samples
            .Select((sample, index) => new IndexedSample(index, sample))
            .AsParallel()
            .WithDegreeOfParallelism(GetDegreeOfParallelism(options.DegreeOfParallelism))
            .Select(s => AnalyzeSample(s, evalHashes, options.RequireBoundingBox))
            .OrderBy(a => a.Index)
            .ToList();

        var acceptedRaw = analyses
            .Where(a => a.Decision == StageASampleDecision.Accepted)
            .ToList();
        var accepted = RemoveDuplicateImages(acceptedRaw);
        var skippedDuplicateImage = acceptedRaw.Count - accepted.Count;

        var classNames = accepted
            .Select(a => a.ClassName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var classMap = classNames
            .Select((name, id) => (name, id))
            .ToDictionary(x => x.name, x => x.id, StringComparer.OrdinalIgnoreCase);

        var splitItems = accepted
            .Select((a, outputIndex) => new StageASplitSample(
                Analysis: a,
                Split: ChooseSplit(a.Sample, options.ValidationRatio),
                ClassId: classMap[a.ClassName!],
                OutputIndex: outputIndex))
            .ToList();

        var classCounts = BuildClassCounts(splitItems);

        string? manifestPath = null;
        string? cleanSamplesPath = null;
        string? dataYamlPath = null;

        if (!options.DryRun)
        {
            Directory.CreateDirectory(options.OutputRoot);

            var exported = await ExportFilesAsync(
                    splitItems,
                    classNames,
                    options.OutputRoot,
                    cancellationToken)
                .ConfigureAwait(false);

            cleanSamplesPath = Path.Combine(options.OutputRoot, "clean_training_samples.json");
            await File.WriteAllTextAsync(
                    cleanSamplesPath,
                    JsonSerializer.Serialize(exported.CleanSamples, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);

            dataYamlPath = WriteDataYaml(options.OutputRoot, classNames, splitItems.Count);

            manifestPath = Path.Combine(options.OutputRoot, "stage_a_manifest.json");
            await WriteManifestAsync(
                    manifestPath,
                    options,
                    analyses,
                    splitItems,
                    classCounts,
                    skippedDuplicateImage,
                    evalHashes.Count,
                    evalHashListSha256,
                    cleanSamplesPath,
                    dataYamlPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new StageAExportResult(
            DryRun: options.DryRun,
            InputSamples: samples.Count,
            ApprovedSamples: analyses.Count(a => a.Decision != StageASampleDecision.NotApproved),
            SkippedNotApproved: analyses.Count(a => a.Decision == StageASampleDecision.NotApproved),
            SkippedEvalSet: analyses.Count(a => a.Decision == StageASampleDecision.EvalSet),
            SkippedMissingOrCorrupt: analyses.Count(a => a.Decision == StageASampleDecision.MissingOrCorrupt),
            SkippedInvalidCode: analyses.Count(a => a.Decision == StageASampleDecision.InvalidCode),
            SkippedWithoutBoundingBox: analyses.Count(a => a.Decision == StageASampleDecision.WithoutBoundingBox),
            SkippedDuplicateImage: skippedDuplicateImage,
            FinalSamples: splitItems.Count,
            TrainSamples: splitItems.Count(s => s.Split == "train"),
            ValidationSamples: splitItems.Count(s => s.Split == "val"),
            EvalHashesCount: evalHashes.Count,
            EvalHashListSha256: evalHashListSha256,
            ManifestPath: manifestPath,
            CleanTrainingSamplesPath: cleanSamplesPath,
            DataYamlPath: dataYamlPath,
            Classes: classCounts);
    }

    private static void ValidateOptions(StageAExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SourceSamplesPath))
            throw new ArgumentException("training_samples.json fehlt.", nameof(options));
        if (!File.Exists(options.SourceSamplesPath))
            throw new FileNotFoundException("training_samples.json nicht gefunden.", options.SourceSamplesPath);
        if (string.IsNullOrWhiteSpace(options.EvalSetRoot))
            throw new ArgumentException("Eval-Set-Ordner fehlt.", nameof(options));
        if (!Directory.Exists(options.EvalSetRoot))
            throw new DirectoryNotFoundException(options.EvalSetRoot);
        if (string.IsNullOrWhiteSpace(options.OutputRoot))
            throw new ArgumentException("OutputRoot fehlt.", nameof(options));
        if (options.ValidationRatio is < 0 or > 1)
            throw new ArgumentException("ValidationRatio muss zwischen 0 und 1 liegen.", nameof(options));
    }

    private static async Task<IReadOnlyList<TrainingSample>> LoadSamplesAsync(
        string sourceSamplesPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(sourceSamplesPath);
        var samples = await JsonSerializer.DeserializeAsync<List<TrainingSample>>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return samples ?? [];
    }

    private static StageASampleAnalysis AnalyzeSample(
        IndexedSample indexed,
        IReadOnlySet<string> evalHashes,
        bool requireBoundingBox)
    {
        var sample = indexed.Sample;

        if (sample.Status != TrainingSampleStatus.Approved)
            return StageASampleAnalysis.NotApproved(indexed);

        var className = NormalizeClassName(sample.Code);
        if (string.IsNullOrWhiteSpace(className))
            return StageASampleAnalysis.InvalidCode(indexed);

        if (requireBoundingBox && !sample.HasBbox)
            return StageASampleAnalysis.WithoutBoundingBox(indexed, className);

        if (string.IsNullOrWhiteSpace(sample.FramePath) ||
            !ImageExtensions.Contains(Path.GetExtension(sample.FramePath)) ||
            !File.Exists(sample.FramePath))
        {
            return StageASampleAnalysis.MissingOrCorrupt(indexed, className);
        }

        string hash;
        try
        {
            hash = ComputeSha256Hex(sample.FramePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return StageASampleAnalysis.MissingOrCorrupt(indexed, className);
        }

        return evalHashes.Contains(hash)
            ? StageASampleAnalysis.EvalSet(indexed, className, hash)
            : StageASampleAnalysis.Accepted(indexed, className, hash);
    }

    private static IReadOnlyList<StageAExportClassCount> BuildClassCounts(
        IReadOnlyList<StageASplitSample> splitItems)
        => splitItems
            .GroupBy(s => s.Analysis.ClassName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new StageAExportClassCount(
                ClassName: g.Key,
                Total: g.Count(),
                Train: g.Count(x => x.Split == "train"),
                Validation: g.Count(x => x.Split == "val")))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<StageASampleAnalysis> RemoveDuplicateImages(
        IReadOnlyList<StageASampleAnalysis> accepted)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<StageASampleAnalysis>(accepted.Count);

        foreach (var item in accepted)
        {
            if (string.IsNullOrWhiteSpace(item.Sha256))
                continue;

            if (!seen.Add(item.Sha256))
                continue;

            unique.Add(item);
        }

        return unique;
    }

    private static async Task<StageAFileExportResult> ExportFilesAsync(
        IReadOnlyList<StageASplitSample> splitItems,
        IReadOnlyList<string> classNames,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        foreach (var split in new[] { "train", "val" })
        {
            Directory.CreateDirectory(Path.Combine(outputRoot, "images", split));
            Directory.CreateDirectory(Path.Combine(outputRoot, "labels", split));
        }

        var cleanSamples = new TrainingSample[splitItems.Count];

        await Parallel.ForEachAsync(
                splitItems,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                },
                async (item, ct) =>
                {
                    var sample = item.Analysis.Sample;
                    var className = item.Analysis.ClassName!;
                    var ext = Path.GetExtension(sample.FramePath);
                    var baseName = $"{item.Analysis.Index:D6}_{SanitizeFileName(sample.SampleId)}_{className}";
                    var imagePath = Path.Combine(outputRoot, "images", item.Split, baseName + ext);
                    var labelPath = Path.Combine(outputRoot, "labels", item.Split, baseName + ".txt");

                    File.Copy(sample.FramePath, imagePath, overwrite: true);

                    var label = BuildYoloLabelLine(item.ClassId, sample);
                    await File.WriteAllTextAsync(labelPath, label + Environment.NewLine, ct)
                        .ConfigureAwait(false);

                    var clone = CloneSample(sample);
                    clone.FramePath = imagePath;
                    cleanSamples[item.OutputIndex] = clone;
                })
            .ConfigureAwait(false);

        return new StageAFileExportResult(cleanSamples);
    }

    private static string WriteDataYaml(
        string outputRoot,
        IReadOnlyList<string> classNames,
        int samplesCount)
    {
        var yamlPath = Path.Combine(outputRoot, "data.yaml");
        var lines = new[]
        {
            "# SewerStudio Stage-A Dataset",
            $"# {samplesCount} Samples, {classNames.Count} Klassen",
            $"path: {outputRoot}",
            "train: images/train",
            "val: images/val",
            "",
            $"nc: {classNames.Count}",
            $"names: [{string.Join(", ", classNames.Select(c => $"'{c}'"))}]",
        };

        File.WriteAllLines(yamlPath, lines, Encoding.UTF8);
        return yamlPath;
    }

    private static async Task WriteManifestAsync(
        string manifestPath,
        StageAExportOptions options,
        IReadOnlyList<StageASampleAnalysis> analyses,
        IReadOnlyList<StageASplitSample> splitItems,
        IReadOnlyList<StageAExportClassCount> classCounts,
        int skippedDuplicateImage,
        int evalHashesCount,
        string evalHashListSha256,
        string cleanSamplesPath,
        string dataYamlPath,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            created_utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            dry_run = false,
            source_samples_path = options.SourceSamplesPath,
            eval_set_root = options.EvalSetRoot,
            output_root = options.OutputRoot,
            hash_algorithm = "sha256",
            eval_hashes_count = evalHashesCount,
            eval_hash_list_sha256 = evalHashListSha256,
            input_samples = analyses.Count,
            approved_samples = analyses.Count(a => a.Decision != StageASampleDecision.NotApproved),
            skipped_not_approved = analyses.Count(a => a.Decision == StageASampleDecision.NotApproved),
            skipped_eval_set = analyses.Count(a => a.Decision == StageASampleDecision.EvalSet),
            skipped_missing_or_corrupt = analyses.Count(a => a.Decision == StageASampleDecision.MissingOrCorrupt),
            skipped_invalid_code = analyses.Count(a => a.Decision == StageASampleDecision.InvalidCode),
            skipped_without_bounding_box = analyses.Count(a => a.Decision == StageASampleDecision.WithoutBoundingBox),
            skipped_duplicate_image = skippedDuplicateImage,
            final_samples = splitItems.Count,
            train_samples = splitItems.Count(s => s.Split == "train"),
            val_samples = splitItems.Count(s => s.Split == "val"),
            clean_training_samples_path = cleanSamplesPath,
            data_yaml_path = dataYamlPath,
            classes = classCounts,
        };

        await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static HashSet<string> LoadEvalImageHashes(string evalSetRoot)
    {
        var manifestPath = Path.Combine(evalSetRoot, "_manifest.json");
        if (File.Exists(manifestPath))
        {
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject();
            var hashes = manifest?["hashes"]?.AsObject();
            if (hashes is not null)
            {
                var fromManifest = hashes
                    .Where(p => p.Key.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value?["sha256"]?.GetValue<string>())
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h!.Trim().ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (fromManifest.Count > 0)
                    return fromManifest;
            }
        }

        var imageRoot = Path.Combine(evalSetRoot, "images");
        if (!Directory.Exists(imageRoot))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(imageRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .AsParallel()
            .Select(ComputeSha256Hex)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeHashListSha256(IReadOnlySet<string> hashes)
    {
        var canonical = string.Join(
            "\n",
            hashes
                .Select(h => h.Trim().ToLowerInvariant())
                .OrderBy(h => h, StringComparer.Ordinal));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ChooseSplit(TrainingSample sample, double validationRatio)
    {
        if (validationRatio <= 0)
            return "train";
        if (validationRatio >= 1)
            return "val";

        var key = string.IsNullOrWhiteSpace(sample.SampleId)
            ? sample.FramePath
            : sample.SampleId;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToUpperInvariant()));
        var value = BitConverter.ToUInt32(hash, 0) / (double)uint.MaxValue;
        return value < validationRatio ? "val" : "train";
    }

    private static string BuildYoloLabelLine(int classId, TrainingSample sample)
    {
        var (xc, yc, w, h) = sample.HasBbox
            ? (
                Clamp01(sample.BboxXCenter!.Value),
                Clamp01(sample.BboxYCenter!.Value),
                Clamp01(sample.BboxWidth!.Value),
                Clamp01(sample.BboxHeight!.Value))
            : (0.5, 0.5, 0.8, 0.8);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1:F6} {2:F6} {3:F6} {4:F6}",
            classId,
            xc,
            yc,
            w,
            h);
    }

    private static double Clamp01(double value)
        => Math.Min(1, Math.Max(0, value));

    private static string NormalizeClassName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var trimmed = code.Trim();
        var dotIdx = trimmed.IndexOf('.');
        if (dotIdx > 0)
            trimmed = trimmed[..dotIdx];

        return trimmed.Length >= 3
            ? trimmed[..3].ToUpperInvariant()
            : trimmed.ToUpperInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }

    private static TrainingSample CloneSample(TrainingSample source)
        => new()
        {
            SampleId = source.SampleId,
            CaseId = source.CaseId,
            Code = source.Code,
            Beschreibung = source.Beschreibung,
            MeterStart = source.MeterStart,
            MeterEnd = source.MeterEnd,
            IsStreckenschaden = source.IsStreckenschaden,
            TimeSeconds = source.TimeSeconds,
            DetectedMeter = source.DetectedMeter,
            MeterSource = source.MeterSource,
            FramePath = source.FramePath,
            Status = source.Status,
            ExportedUtc = source.ExportedUtc,
            Notes = source.Notes,
            TruthMeterCenter = source.TruthMeterCenter,
            OdsDeltaMeters = source.OdsDeltaMeters,
            HasOsdMismatch = source.HasOsdMismatch,
            Signature = source.Signature,
            FrameIndex = source.FrameIndex,
            MatchLevel = source.MatchLevel,
            KiCode = source.KiCode,
            SourceType = source.SourceType,
            CodeMeta = GroundTruthProtocolEntryMapper.CloneCodeMeta(source.CodeMeta),
            TechniqueGrade = source.TechniqueGrade,
            AdditionalFramePaths = source.AdditionalFramePaths is null ? null : [.. source.AdditionalFramePaths],
            KbIndexState = source.KbIndexState,
            BboxXCenter = source.BboxXCenter,
            BboxYCenter = source.BboxYCenter,
            BboxWidth = source.BboxWidth,
            BboxHeight = source.BboxHeight,
        };

    private static int GetDegreeOfParallelism(int requested)
        => requested > 0
            ? requested
            : Math.Max(1, Environment.ProcessorCount - 1);

    private sealed record IndexedSample(int Index, TrainingSample Sample);

    private sealed record StageASampleAnalysis(
        int Index,
        TrainingSample Sample,
        StageASampleDecision Decision,
        string? ClassName,
        string? Sha256)
    {
        public static StageASampleAnalysis NotApproved(IndexedSample source)
            => new(source.Index, source.Sample, StageASampleDecision.NotApproved, null, null);

        public static StageASampleAnalysis InvalidCode(IndexedSample source)
            => new(source.Index, source.Sample, StageASampleDecision.InvalidCode, null, null);

        public static StageASampleAnalysis MissingOrCorrupt(IndexedSample source, string? className)
            => new(source.Index, source.Sample, StageASampleDecision.MissingOrCorrupt, className, null);

        public static StageASampleAnalysis WithoutBoundingBox(IndexedSample source, string className)
            => new(source.Index, source.Sample, StageASampleDecision.WithoutBoundingBox, className, null);

        public static StageASampleAnalysis EvalSet(IndexedSample source, string className, string sha256)
            => new(source.Index, source.Sample, StageASampleDecision.EvalSet, className, sha256);

        public static StageASampleAnalysis Accepted(IndexedSample source, string className, string sha256)
            => new(source.Index, source.Sample, StageASampleDecision.Accepted, className, sha256);
    }

    private sealed record StageASplitSample(
        StageASampleAnalysis Analysis,
        string Split,
        int ClassId,
        int OutputIndex);

    private sealed record StageAFileExportResult(
        IReadOnlyList<TrainingSample> CleanSamples);

    private enum StageASampleDecision
    {
        Accepted,
        NotApproved,
        EvalSet,
        MissingOrCorrupt,
        InvalidCode,
        WithoutBoundingBox,
    }
}
