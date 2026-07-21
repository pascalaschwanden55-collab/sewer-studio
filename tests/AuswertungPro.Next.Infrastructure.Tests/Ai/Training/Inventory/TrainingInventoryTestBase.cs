using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public abstract class TrainingInventoryTestBase : IDisposable
{
    protected string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "training-inventory-tests",
        Guid.NewGuid().ToString("N"));

    protected static TrainingDataInventoryService CreateService()
        => new();

    protected TrainingDataInventoryRequest CreateRequest(
        IReadOnlyList<string> searchRoots,
        bool computeAssetHashes = true)
    {
        var evalImages = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "images")).FullName;
        var marker = Path.Combine(evalImages, "900-901_marker.png");
        if (!File.Exists(marker))
            File.WriteAllBytes(marker, [251, 252, 253]);
        var evalImageCount = Directory.EnumerateFiles(evalImages, "*", SearchOption.TopDirectoryOnly).Count();
        WriteCompleteEvalSet(
            Path.Combine(Root, "eval_set"),
            Enumerable.Repeat("900-901", evalImageCount).ToArray());

        return new TrainingDataInventoryRequest
        {
            KnowledgeRoot = Root,
            EvalSetRoot = Path.Combine(Root, "eval_set"),
            SearchRoots = searchRoots,
            ProtectedRoots = [Path.Combine(Root, "eval_set")],
            IncludeBackups = false,
            ComputeAssetHashes = computeAssetHashes
        };
    }

    protected void WriteCurrentSources(params TeacherAnnotation?[] annotations)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(
            Path.Combine(Root, "teacher_annotations.json"),
            JsonSerializer.Serialize(annotations, JsonDefaults.IndentedCamel));
        File.WriteAllText(Path.Combine(Root, "training_samples.json"), "[]");
    }

    protected void WriteRawCurrentSources(string teacherJson, string trainingSamplesJson)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "teacher_annotations.json"), teacherJson);
        File.WriteAllText(Path.Combine(Root, "training_samples.json"), trainingSamplesJson);
    }

    protected static TeacherAnnotation CreateAnnotation(
        string id,
        string holding,
        string framePath,
        double width,
        double height)
        => new()
        {
            AnnotationId = id,
            VsaCode = "BABBB",
            HaltungName = holding,
            FullFramePath = framePath,
            BoundingBox = new NormalizedBoundingBox
            {
                XCenter = 0.5,
                YCenter = 0.5,
                Width = width,
                Height = height
            }
        };

    protected static string[] SnapshotFiles(string root)
        => Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => $"{Path.GetRelativePath(root, path)}|{ComputeHash(path)}")
                .ToArray()
            : [];

    protected static void WriteCompleteEvalSet(
        string setRoot,
        params string[] holdingKeys)
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(setRoot, "images")).FullName;
        var imageFiles = Directory.EnumerateFiles(imageRoot, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (imageFiles.Length != holdingKeys.Length)
            throw new InvalidOperationException("Test-Eval-Set braucht genau einen Kandidaten je Bild.");
        var candidatesPath = Path.Combine(setRoot, "_candidates.json");
        File.WriteAllText(
            candidatesPath,
            JsonSerializer.Serialize(
                holdingKeys.Select((key, index) => new
                {
                    id = $"candidate-{index}",
                    frame_path = imageFiles[index],
                    haltung_key = key
                }),
                JsonDefaults.IndentedCamel));
        var hashes = imageFiles
            .ToDictionary(
                path => $"images/{Path.GetFileName(path)}",
                path => (object)new
                {
                    sha256 = ComputeHash(path),
                    size_bytes = new FileInfo(path).Length
                },
                StringComparer.OrdinalIgnoreCase);
        hashes["_candidates.json"] = new
        {
            sha256 = ComputeHash(candidatesPath),
            size_bytes = new FileInfo(candidatesPath).Length
        };

        File.WriteAllText(
            Path.Combine(setRoot, "_manifest.json"),
            JsonSerializer.Serialize(
                new
                {
                    frozen = true,
                    hash_algorithm = "sha256",
                    hashes_count = hashes.Count,
                    candidates_count = holdingKeys.Length,
                    hashes
                },
                JsonDefaults.IndentedCamel));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);

        GC.SuppressFinalize(this);
    }

    protected static string ComputeHash(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
