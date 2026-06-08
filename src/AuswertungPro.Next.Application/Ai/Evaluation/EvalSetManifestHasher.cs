using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalSetHashEntry(
    string RelativePath,
    long SizeBytes,
    string Sha256Hex);

public sealed record EvalSetHashResult(
    string Algorithm,
    int HashesCount,
    IReadOnlyList<EvalSetHashEntry> Hashes);

/// <summary>
/// Erzeugt einen stabilen SHA-256-Hash-Block fuer ein eingefrorenes Eval-Set.
/// Geschuetzt werden die unveraenderlichen Eval-Artefakte: images/, labels/ und
/// _candidates.json. Ergebnisdateien wie metrics_*.csv bleiben bewusst draussen.
/// </summary>
public static class EvalSetManifestHasher
{
    public const string Algorithm = "sha256";
    private const string ManifestFileName = "_manifest.json";
    private const string CandidatesFileName = "_candidates.json";

    public static EvalSetHashResult ComputeAndStoreHashes(string evalSetRoot)
    {
        var result = ComputeHashes(evalSetRoot);
        var manifestPath = Path.Combine(evalSetRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Eval-Set-Manifest nicht gefunden.", manifestPath);

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidDataException("Eval-Set-Manifest ist kein JSON-Objekt.");

        var counts = CountEvalSetArtifacts(evalSetRoot);
        manifest["approved"] = counts.ApprovedCandidates;
        manifest["candidates_count"] = counts.CandidateCount;
        manifest["images_count"] = counts.ImagesCount;
        manifest["labels_count"] = counts.LabelsCount;
        manifest["hash_algorithm"] = result.Algorithm;
        manifest["hashes_count"] = result.HashesCount;
        manifest["hashes_generated_utc"] = DateTimeOffset.UtcNow.ToString("O");

        var hashes = new JsonObject();
        foreach (var entry in result.Hashes)
        {
            hashes[entry.RelativePath] = new JsonObject
            {
                ["sha256"] = entry.Sha256Hex,
                ["size_bytes"] = entry.SizeBytes,
            };
        }
        manifest["hashes"] = hashes;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(manifestPath, manifest.ToJsonString(options));
        return result;
    }

    public static EvalSetHashResult ComputeHashes(string evalSetRoot)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
            throw new ArgumentException("Eval-Set-Pfad fehlt.", nameof(evalSetRoot));
        if (!Directory.Exists(evalSetRoot))
            throw new DirectoryNotFoundException(evalSetRoot);

        var entries = EnumerateStableEvalFiles(evalSetRoot)
            .Select(path => new EvalSetHashEntry(
                RelativePath: ToRelativeSlashPath(evalSetRoot, path),
                SizeBytes: new FileInfo(path).Length,
                Sha256Hex: ComputeSha256Hex(path)))
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToList();

        return new EvalSetHashResult(Algorithm, entries.Count, entries);
    }

    private static IEnumerable<string> EnumerateStableEvalFiles(string evalSetRoot)
    {
        var candidates = Path.Combine(evalSetRoot, CandidatesFileName);
        if (File.Exists(candidates))
            yield return candidates;

        foreach (var subDir in new[] { "images", "labels" })
        {
            var dir = Path.Combine(evalSetRoot, subDir);
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                yield return file;
        }
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToRelativeSlashPath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static EvalSetArtifactCounts CountEvalSetArtifacts(string evalSetRoot)
    {
        var candidates = CountCandidates(Path.Combine(evalSetRoot, CandidatesFileName));
        return new EvalSetArtifactCounts(
            CandidateCount: candidates.Total,
            ApprovedCandidates: candidates.Approved,
            ImagesCount: CountFiles(Path.Combine(evalSetRoot, "images")),
            LabelsCount: CountFiles(Path.Combine(evalSetRoot, "labels")));
    }

    private static (int Total, int Approved) CountCandidates(string candidatesPath)
    {
        if (!File.Exists(candidatesPath))
            return (0, 0);

        using var doc = JsonDocument.Parse(File.ReadAllText(candidatesPath));
        var root = doc.RootElement;
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.ValueKind == JsonValueKind.Object &&
              root.TryGetProperty("candidates", out var nested) &&
              nested.ValueKind == JsonValueKind.Array
                ? nested
                : default;

        if (array.ValueKind != JsonValueKind.Array)
            return (0, 0);

        var total = 0;
        var approved = 0;
        foreach (var item in array.EnumerateArray())
        {
            total++;
            var status = item.ValueKind == JsonValueKind.Object &&
                         item.TryGetProperty("status", out var statusElement) &&
                         statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(status) ||
                string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                approved++;
            }
        }

        return (total, approved);
    }

    private static int CountFiles(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count()
            : 0;

    private sealed record EvalSetArtifactCounts(
        int CandidateCount,
        int ApprovedCandidates,
        int ImagesCount,
        int LabelsCount);
}
