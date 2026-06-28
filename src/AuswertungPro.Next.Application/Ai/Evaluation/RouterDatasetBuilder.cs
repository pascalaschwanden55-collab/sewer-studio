using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record RouterDatasetBuilderOptions(
    IReadOnlyList<string> SourceDatasetRoots,
    string OutputRoot,
    string? EvalSetRoot,
    bool DryRun,
    IReadOnlyList<string>? SourceFileLists = null,
    double SourceFileListValidationRatio = 0,
    int MaxPerClassPerSplit = 0);

public sealed record RouterDatasetBuilderResult(
    int Copied,
    int SkippedEvalSet,
    int SkippedUnknownClass,
    int SkippedMissingFiles,
    int SkippedClassCap,
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

    public static RouterDatasetBuilderResult Build(RouterDatasetBuilderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceFileLists = options.SourceFileLists ?? Array.Empty<string>();
        if (options.SourceDatasetRoots.Count == 0 && sourceFileLists.Count == 0)
            throw new ArgumentException("Mindestens ein Quell-Dataset oder eine Pfadliste ist noetig.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputRoot))
            throw new ArgumentException("OutputRoot fehlt.", nameof(options));

        var evalHashes = LoadEvalSetImageHashes(options.EvalSetRoot);
        var counts = new Dictionary<(string Split, string RouterClass), int>();
        var copied = 0;
        var skippedEvalSet = 0;
        var skippedUnknownClass = 0;
        var skippedMissingFiles = 0;
        var skippedClassCap = 0;

        foreach (var source in EnumerateAllSources(options.SourceDatasetRoots, sourceFileLists, options.SourceFileListValidationRatio))
        {
            if (!File.Exists(source.Path))
            {
                skippedMissingFiles++;
                continue;
            }

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

            var key = (source.Split, routerClass);
            if (options.MaxPerClassPerSplit > 0 &&
                counts.GetValueOrDefault(key) >= options.MaxPerClassPerSplit)
            {
                skippedClassCap++;
                continue;
            }

            copied++;
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

        return new RouterDatasetBuilderResult(
            copied,
            skippedEvalSet,
            skippedUnknownClass,
            skippedMissingFiles,
            skippedClassCap,
            counts
                .Select(c => new RouterDatasetBuilderClassCount(c.Key.Split, c.Key.RouterClass, c.Value))
                .OrderBy(c => c.Split, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(c => c.Count)
                .ThenBy(c => c.RouterClass, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public static string? MapSourceClassToRouterClass(string sourceClass)
        => RouterSourceClassResolver.MapSourceClassToRouterClass(sourceClass);

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

    private static IReadOnlyList<SourceImage> EnumerateAllSources(
        IReadOnlyList<string> sourceDatasetRoots,
        IReadOnlyList<string> sourceFileLists,
        double sourceFileListValidationRatio)
        => sourceDatasetRoots
            .SelectMany(EnumerateSourceImages)
            .Concat(sourceFileLists.SelectMany(path => EnumerateSourceFileList(path, sourceFileListValidationRatio)))
            .ToList();

    private static IReadOnlyList<SourceImage> EnumerateSourceFileList(
        string fileListPath,
        double validationRatio)
    {
        if (string.IsNullOrWhiteSpace(fileListPath))
            return Array.Empty<SourceImage>();
        if (!File.Exists(fileListPath))
            throw new FileNotFoundException("Pfadliste nicht gefunden.", fileListPath);

        return File.ReadLines(fileListPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase) ||
                           line.StartsWith(@"C:\", StringComparison.OrdinalIgnoreCase))
            .Where(line => IsImageFile(line))
            .Select(path => new SourceImage(
                Path: path,
                Split: ChooseSplit(path, validationRatio),
                SourceClass: ExtractClassFromFileName(path) ?? ""))
            .ToList();
    }

    private static string ChooseSplit(string path, double validationRatio)
        => YoloDatasetNaming.ChooseSplit(path, validationRatio);

    private static string? ExtractClassFromFileName(string path)
        => RouterSourceClassResolver.ExtractClassFromFileName(path);

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
