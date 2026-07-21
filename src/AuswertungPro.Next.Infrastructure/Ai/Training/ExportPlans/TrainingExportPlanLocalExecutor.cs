using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Lokaler Ausfuehrer desselben unveraenderlichen Plans. Er schreibt zuerst in
/// einen Arbeitsordner und veroeffentlicht erst nach vollstaendiger Pruefung.
/// </summary>
public sealed class TrainingExportPlanLocalExecutor : ITrainingExportPlanLocalExecutor
{
    private const string StagingDirectoryName = ".staging";
    private const string ReceiptFileName = "_export_receipt.json";
    private const string ManifestFileName = "manifest.json";
    private const string ClassesFileName = "classes.txt";
    private const string DataYamlFileName = "data.yaml";
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new() { WriteIndented = true };

    public async Task<TrainingExportExecutionResult> ExecuteAsync(
        TrainingExportPlanBundle bundle,
        string datasetRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetRoot);
        TrainingExportPlanValidator.Validate(bundle.Plan);
        if (bundle.Plan.Images.Count == 0)
            throw new TrainingExportPlanException("Der Exportplan enthaelt keine auszugebenden Bilder.");

        var root = EnsureSafeDirectory(Path.GetFullPath(datasetRoot), create: true);
        var target = Path.Combine(root, bundle.Plan.PlanId);
        EnsureDirectChild(target, root, "Exportziel");
        var files = await BuildFilesAsync(bundle, cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(target) || File.Exists(target))
        {
            ValidateCompleteDataset(target, files);
            return CreateResult(bundle.Plan, target, TrainingExportExecutionStatus.AlreadyComplete);
        }

        var stagingRoot = EnsureSafeDirectory(Path.Combine(root, StagingDirectoryName), create: true);
        EnsureDirectChild(stagingRoot, root, "Arbeitswurzel");
        var stage = Path.Combine(stagingRoot, $"{bundle.Plan.PlanId}.{Guid.NewGuid():N}.tmp");
        EnsureDirectChild(stage, stagingRoot, "Arbeitsordner");
        Directory.CreateDirectory(stage);
        try
        {
            await WriteStageAsync(stage, files, cancellationToken).ConfigureAwait(false);
            ValidateCompleteDataset(stage, files);
            try
            {
                Directory.Move(stage, target);
            }
            catch (IOException) when (Directory.Exists(target))
            {
                ValidateCompleteDataset(target, files);
                return CreateResult(bundle.Plan, target, TrainingExportExecutionStatus.AlreadyComplete);
            }

            return CreateResult(bundle.Plan, target, TrainingExportExecutionStatus.Created);
        }
        finally
        {
            DeleteOwnStageIfPresent(stage, stagingRoot);
        }
    }

    private static async Task<PlannedDatasetFiles> BuildFilesAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken)
    {
        var imageFiles = new List<PlannedFile>(bundle.Plan.Images.Count);
        var labelFiles = new List<PlannedFile>(bundle.Plan.Images.Count);
        foreach (var image in bundle.Plan.Images)
        {
            if (!bundle.SourcePathsByImageSha256.TryGetValue(image.ImageSha256, out var sourcePath))
                throw new TrainingExportPlanException($"Originalpfad fuer Bild {image.ImageSha256} fehlt.");
            var imageBytes = await TrainingExportSidecarRequestBuilder.ReadStableVerifiedImageAsync(
                    sourcePath,
                    image.ImageSha256,
                    cancellationToken)
                .ConfigureAwait(false);
            TrainingExportImageFormatValidator.Validate(imageBytes, image.TargetFileName);
            var split = image.Target == TrainingExportTarget.Train ? "train" : "val";
            imageFiles.Add(new PlannedFile(
                $"images/{split}/{image.TargetFileName}",
                imageBytes,
                image.ImageSha256));
            var labelBytes = BuildLabelBytes(image.Labels);
            labelFiles.Add(new PlannedFile(
                $"labels/{split}/{Path.GetFileNameWithoutExtension(image.TargetFileName)}.txt",
                labelBytes,
                Hash(labelBytes)));
        }

        var classesBytes = Encoding.UTF8.GetBytes(string.Join('\n', bundle.Plan.Classes) + "\n");
        var dataYamlBytes = BuildDataYamlBytes(bundle.Plan.Classes);
        var manifestBytes = TrainingExportPlanSerializer.SerializeManifest(bundle.Plan);
        var receipt = new ExportReceipt(
            ClassCount: bundle.Plan.Classes.Count,
            ClassMapVersion: bundle.Plan.ClassMapVersion,
            ClassesSha256: Hash(classesBytes),
            DataYamlSha256: Hash(dataYamlBytes),
            Images: imageFiles
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => new ReceiptFile(file.RelativePath, file.Sha256))
                .ToArray(),
            Labels: labelFiles
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => new ReceiptFile(file.RelativePath, file.Sha256))
                .ToArray(),
            ManifestSha256: Hash(manifestBytes),
            PlanId: bundle.Plan.PlanId,
            PlanSha256: bundle.Plan.PlanId,
            RegistryHash: bundle.Plan.RegistryHash,
            SchemaVersion: TrainingExportPlan.CurrentSchemaVersion,
            TotalSamples: bundle.Plan.Images.Count,
            TrainCount: bundle.Plan.Images.Count(image => image.Target == TrainingExportTarget.Train),
            ValidationCount: bundle.Plan.Images.Count(image => image.Target == TrainingExportTarget.Validation),
            VsaManifestHash: bundle.Plan.VsaManifestHash);
        var receiptJson = JsonSerializer.Serialize(receipt, ReceiptJsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var receiptBytes = Encoding.UTF8.GetBytes(receiptJson + "\n");
        return new PlannedDatasetFiles(
            imageFiles,
            labelFiles,
            new PlannedFile(ClassesFileName, classesBytes, Hash(classesBytes)),
            new PlannedFile(DataYamlFileName, dataYamlBytes, Hash(dataYamlBytes)),
            new PlannedFile(ManifestFileName, manifestBytes, Hash(manifestBytes)),
            new PlannedFile(ReceiptFileName, receiptBytes, Hash(receiptBytes)),
            receipt);
    }

    private static async Task WriteStageAsync(
        string stage,
        PlannedDatasetFiles files,
        CancellationToken cancellationToken)
    {
        foreach (var category in new[] { "images", "labels" })
        foreach (var split in new[] { "train", "val" })
            Directory.CreateDirectory(Path.Combine(stage, category, split));

        foreach (var file in files.AllFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = ResolveRelativeFile(stage, file.RelativePath);
            await File.WriteAllBytesAsync(target, file.Bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateCompleteDataset(string dataset, PlannedDatasetFiles files)
    {
        var fullDataset = EnsureSafeDirectory(dataset, create: false);
        var allowedRootEntries = new HashSet<string>(
            ["images", "labels", ClassesFileName, DataYamlFileName, ManifestFileName, ReceiptFileName],
            StringComparer.Ordinal);
        var actualRootEntries = Directory.EnumerateFileSystemEntries(fullDataset, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualRootEntries.SetEquals(allowedRootEntries))
            throw Conflict("Bestehender Datensatz enthaelt unerwartete oder fehlende Haupteintraege.");

        ValidateCategoryFiles(fullDataset, "images", files.Images.Select(file => file.RelativePath));
        ValidateCategoryFiles(fullDataset, "labels", files.Labels.Select(file => file.RelativePath));
        foreach (var file in files.AllFiles.Where(file => file.RelativePath != ReceiptFileName))
        {
            var path = ResolveRelativeFile(fullDataset, file.RelativePath);
            EnsureSafeFile(path, fullDataset);
            if (!HashFile(path).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw Conflict($"Datei stimmt nicht mit dem Plan ueberein: {file.RelativePath}");
        }

        var receiptPath = ResolveRelativeFile(fullDataset, ReceiptFileName);
        EnsureSafeFile(receiptPath, fullDataset);
        try
        {
            using var actual = JsonDocument.Parse(File.ReadAllBytes(receiptPath));
            using var expected = JsonDocument.Parse(
                JsonSerializer.SerializeToUtf8Bytes(files.Receipt, ReceiptJsonOptions));
            if (!JsonElement.DeepEquals(actual.RootElement, expected.RootElement))
                throw Conflict("Exportbeleg stimmt nicht mit dem Plan ueberein.");
        }
        catch (JsonException ex)
        {
            throw Conflict($"Exportbeleg ist ungueltig: {ex.Message}");
        }
    }

    private static void ValidateCategoryFiles(
        string dataset,
        string category,
        IEnumerable<string> expectedRelativePaths)
    {
        var categoryRoot = EnsureSafeDirectory(Path.Combine(dataset, category), create: false);
        var rootEntries = Directory.EnumerateFileSystemEntries(categoryRoot)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        if (!rootEntries.SetEquals(["train", "val"]))
            throw Conflict($"Datensatzordner {category} hat unvollstaendige Splits.");

        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var split in new[] { "train", "val" })
        {
            var splitRoot = EnsureSafeDirectory(Path.Combine(categoryRoot, split), create: false);
            foreach (var path in Directory.EnumerateFileSystemEntries(splitRoot, "*", SearchOption.TopDirectoryOnly))
            {
                EnsureSafeFile(path, dataset);
                actual.Add($"{category}/{split}/{Path.GetFileName(path)}");
            }
        }
        if (!actual.SetEquals(expectedRelativePaths))
            throw Conflict($"Datensatzdateien unter {category} stimmen nicht mit dem Plan ueberein.");
    }

    private static byte[] BuildLabelBytes(IReadOnlyList<TrainingExportPlannedLabel> labels)
    {
        if (labels.Count == 0)
            return [];
        var lines = labels.Select(label => string.Create(
            CultureInfo.InvariantCulture,
            $"{label.ClassId} {label.BoundingBox.XCenter:F6} {label.BoundingBox.YCenter:F6} " +
            $"{label.BoundingBox.Width:F6} {label.BoundingBox.Height:F6}"));
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
    }

    private static byte[] BuildDataYamlBytes(IReadOnlyList<string> classes)
    {
        var lines = new List<string>
        {
            "path: .",
            "train: images/train",
            "val: images/val",
            $"nc: {classes.Count}",
            "names:"
        };
        lines.AddRange(classes.Select((name, index) => $"  {index}: {name}"));
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
    }

    private static TrainingExportExecutionResult CreateResult(
        TrainingExportPlan plan,
        string target,
        TrainingExportExecutionStatus status)
        => new(
            plan.PlanId,
            plan.PlanId,
            status,
            plan.Images.Count,
            plan.Images.Count(image => image.Target == TrainingExportTarget.Train),
            plan.Images.Count(image => image.Target == TrainingExportTarget.Validation),
            plan.Classes.Count,
            target,
            Path.Combine(target, DataYamlFileName),
            Path.Combine(target, ManifestFileName),
            plan.Images.Select(image => image.ImageSha256).ToArray());

    private static string EnsureSafeDirectory(string path, bool create)
    {
        var fullPath = Path.GetFullPath(path);
        if (create)
            Directory.CreateDirectory(fullPath);
        if (!Directory.Exists(fullPath))
            throw Conflict($"Ordner fehlt: {fullPath}");
        var reparsePoint = TrainingInventoryPaths.FindReparsePoint(fullPath);
        if (reparsePoint is not null)
            throw Conflict($"Ordnerpfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
        return fullPath;
    }

    private static void EnsureSafeFile(string path, string dataset)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)
            || TrainingInventoryPaths.FindReparsePoint(fullPath) is not null
            || !IsWithin(fullPath, dataset))
        {
            throw Conflict($"Unsichere oder fehlende Datensatzdatei: {path}");
        }
    }

    private static string ResolveRelativeFile(string root, string slashPath)
    {
        var path = Path.GetFullPath(Path.Combine(root, slashPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(path, root))
            throw Conflict($"Unsicherer relativer Exportpfad: {slashPath}");
        return path;
    }

    private static void EnsureDirectChild(string child, string parent, string label)
    {
        var fullChild = Path.GetFullPath(child);
        var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(fullChild), fullParent, StringComparison.OrdinalIgnoreCase))
            throw Conflict($"{label} liegt ausserhalb der vorgesehenen Wurzel.");
    }

    private static bool IsWithin(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteOwnStageIfPresent(string stage, string stagingRoot)
    {
        if (!Directory.Exists(stage))
            return;
        EnsureDirectChild(stage, stagingRoot, "Arbeitsordner");
        if (TrainingInventoryPaths.FindReparsePoint(stage) is not null)
            throw Conflict("Unsicherer Arbeitsordner wird nicht rekursiv geloescht.");
        Directory.Delete(stage, recursive: true);
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static TrainingExportPlanException Conflict(string message)
        => new($"Lokaler Exportkonflikt: {message}");

    private sealed record PlannedFile(string RelativePath, byte[] Bytes, string Sha256);

    private sealed record PlannedDatasetFiles(
        IReadOnlyList<PlannedFile> Images,
        IReadOnlyList<PlannedFile> Labels,
        PlannedFile Classes,
        PlannedFile DataYaml,
        PlannedFile Manifest,
        PlannedFile ReceiptFile,
        ExportReceipt Receipt)
    {
        public IEnumerable<PlannedFile> AllFiles => Images
            .Concat(Labels)
            .Append(Classes)
            .Append(DataYaml)
            .Append(Manifest)
            .Append(ReceiptFile);
    }

    private sealed record ReceiptFile(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("sha256")] string Sha256);

    // Reihenfolge absichtlich alphabetisch: identische Bytes wie json.dumps(sort_keys=True)
    // im Sidecar.
    private sealed record ExportReceipt(
        [property: JsonPropertyName("class_count")] int ClassCount,
        [property: JsonPropertyName("class_map_version")] int ClassMapVersion,
        [property: JsonPropertyName("classes_sha256")] string ClassesSha256,
        [property: JsonPropertyName("data_yaml_sha256")] string DataYamlSha256,
        [property: JsonPropertyName("images")] IReadOnlyList<ReceiptFile> Images,
        [property: JsonPropertyName("labels")] IReadOnlyList<ReceiptFile> Labels,
        [property: JsonPropertyName("manifest_sha256")] string ManifestSha256,
        [property: JsonPropertyName("plan_id")] string PlanId,
        [property: JsonPropertyName("plan_sha256")] string PlanSha256,
        [property: JsonPropertyName("registry_hash")] string RegistryHash,
        [property: JsonPropertyName("schema_version")] string SchemaVersion,
        [property: JsonPropertyName("total_samples")] int TotalSamples,
        [property: JsonPropertyName("train_count")] int TrainCount,
        [property: JsonPropertyName("val_count")] int ValidationCount,
        [property: JsonPropertyName("vsa_manifest_hash")] string VsaManifestHash);
}
