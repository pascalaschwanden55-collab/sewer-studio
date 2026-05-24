using System.Security.Cryptography;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record RouterDatasetBuilderOptions(
    IReadOnlyList<string> SourceDatasetRoots,
    string OutputRoot,
    string? EvalSetRoot,
    bool DryRun);

public sealed record RouterDatasetBuilderResult(
    int Copied,
    int SkippedEvalSet,
    int SkippedUnknownClass,
    IReadOnlyList<RouterDatasetBuilderClassCount> Classes);

public sealed record RouterDatasetBuilderClassCount(
    string Split,
    string RouterClass,
    int Count);

public static class RouterDatasetBuilder
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp",
    };

    private static readonly HashSet<string> KnownRouterClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "leer",
        "beginn_ende",
        "wasserstand",
        "anschluss",
        "oberflaeche",
        "riss_bruch",
        "versatz",
        "ablagerung",
        "wurzeln",
        "deformation",
        "dichtung",
        "infiltration",
        "sonstiges",
    };

    private static readonly Dictionary<string, string> KnownSourceClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["empty"] = "leer",
        ["negative"] = "leer",
        ["no_damage"] = "leer",
        ["no_schaden"] = "leer",
        ["kein_schaden"] = "leer",
        ["meta"] = "beginn_ende",
        ["start_ende"] = "beginn_ende",
        ["rohranfang_ende"] = "beginn_ende",
        ["oberflaeche"] = "oberflaeche",
        ["versatz"] = "versatz",
        ["riss_bruch"] = "riss_bruch",
        ["rissbruch"] = "riss_bruch",
        ["ablagerung"] = "ablagerung",
        ["anschluss"] = "anschluss",
        ["infiltration"] = "infiltration",
        ["deformation"] = "deformation",
        ["dichtung"] = "dichtung",
        ["wurzeln"] = "wurzeln",
    };

    public static RouterDatasetBuilderResult Build(RouterDatasetBuilderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SourceDatasetRoots.Count == 0)
            throw new ArgumentException("Mindestens ein Quell-Dataset ist noetig.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputRoot))
            throw new ArgumentException("OutputRoot fehlt.", nameof(options));

        var evalHashes = LoadEvalSetImageHashes(options.EvalSetRoot);
        var counts = new Dictionary<(string Split, string RouterClass), int>();
        var copied = 0;
        var skippedEvalSet = 0;
        var skippedUnknownClass = 0;

        foreach (var sourceRoot in options.SourceDatasetRoots)
        {
            foreach (var source in EnumerateSourceImages(sourceRoot))
            {
                var routerClass = MapSourceClassToRouterClass(source.SourceClass);
                if (routerClass is null)
                {
                    skippedUnknownClass++;
                    continue;
                }

                var hash = ComputeSha256(source.Path);
                if (evalHashes.Contains(hash))
                {
                    skippedEvalSet++;
                    continue;
                }

                copied++;
                var key = (source.Split, routerClass);
                counts[key] = counts.GetValueOrDefault(key) + 1;

                if (options.DryRun)
                    continue;

                var dest = EnsureUniquePath(Path.Combine(
                    options.OutputRoot,
                    source.Split,
                    routerClass,
                    Path.GetFileName(source.Path)));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source.Path, dest);
            }
        }

        return new RouterDatasetBuilderResult(
            copied,
            skippedEvalSet,
            skippedUnknownClass,
            counts
                .Select(c => new RouterDatasetBuilderClassCount(c.Key.Split, c.Key.RouterClass, c.Value))
                .OrderBy(c => c.Split, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(c => c.Count)
                .ThenBy(c => c.RouterClass, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public static string? MapSourceClassToRouterClass(string sourceClass)
    {
        if (string.IsNullOrWhiteSpace(sourceClass))
            return null;

        var normalized = NormalizeClassName(sourceClass);
        if (KnownRouterClasses.Contains(normalized))
            return normalized;
        if (KnownSourceClasses.TryGetValue(normalized, out var mapped))
            return mapped;

        var code = EvalSetBenchmarkDataset.NormalizeCode(sourceClass);
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var routerClass = EvalSetRouterPlanner.MapExpectedCodeToRouterClass(code);
        return string.Equals(routerClass, "sonstiges", StringComparison.OrdinalIgnoreCase)
            ? null
            : routerClass;
    }

    private static IReadOnlyList<SourceImage> EnumerateSourceImages(string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
            return Array.Empty<SourceImage>();
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException(sourceRoot);

        var splitRoots = new[]
            {
                ("train", Path.Combine(sourceRoot, "train")),
                ("val", Path.Combine(sourceRoot, "val")),
            }
            .Where(s => Directory.Exists(s.Item2))
            .ToList();

        if (splitRoots.Count == 0)
            splitRoots.Add(("train", sourceRoot));

        return splitRoots
            .SelectMany(split => Directory
                .EnumerateDirectories(split.Item2)
                .SelectMany(classDir => Directory
                    .EnumerateFiles(classDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsImageFile)
                    .Select(path => new SourceImage(
                        Path: path,
                        Split: split.Item1,
                        SourceClass: Path.GetFileName(classDir) ?? ""))))
            .ToList();
    }

    private static HashSet<string> LoadEvalSetImageHashes(string? evalSetRoot)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(evalSetRoot))
            return hashes;

        var imageRoot = Path.Combine(evalSetRoot, "images");
        if (!Directory.Exists(imageRoot))
            return hashes;

        foreach (var path in Directory.EnumerateFiles(imageRoot, "*.*", SearchOption.TopDirectoryOnly).Where(IsImageFile))
            hashes.Add(ComputeSha256(path));

        return hashes;
    }

    private static bool IsImageFile(string path)
        => ImageExtensions.Contains(Path.GetExtension(path));

    private static string NormalizeClassName(string value)
        => value.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private sealed record SourceImage(
        string Path,
        string Split,
        string SourceClass);
}
