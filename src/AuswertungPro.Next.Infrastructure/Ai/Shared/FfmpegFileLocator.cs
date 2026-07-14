using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Shared;

/// <summary>
/// Sucht FFmpeg und FFprobe über Einstellung, bekannte Windows-Installationen und PATH.
/// </summary>
public sealed class FfmpegFileLocator : IFfmpegExecutableLocator
{
    internal const string EnvironmentVariableName = "SEWERSTUDIO_FFMPEG";

    private readonly object _sync = new();
    private readonly Func<string, string?> _environmentVariableReader;
    private readonly Func<Environment.SpecialFolder, string> _specialFolderPathResolver;
    private readonly string _manualFfmpegPath;
    private string? _cachedFfmpegPath;

    public FfmpegFileLocator()
        : this(
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath,
            @"C:\ffmpeg\bin\ffmpeg.exe")
    {
    }

    public FfmpegFileLocator(
        Func<string, string?> environmentVariableReader,
        Func<Environment.SpecialFolder, string> specialFolderPathResolver,
        string manualFfmpegPath)
    {
        _environmentVariableReader = environmentVariableReader
            ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        _specialFolderPathResolver = specialFolderPathResolver
            ?? throw new ArgumentNullException(nameof(specialFolderPathResolver));
        _manualFfmpegPath = manualFfmpegPath
            ?? throw new ArgumentNullException(nameof(manualFfmpegPath));
    }

    public string ResolveFfmpeg()
    {
        var configured = _environmentVariableReader(EnvironmentVariableName)?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;

        lock (_sync)
        {
            if (_cachedFfmpegPath is not null)
                return _cachedFfmpegPath;

            _cachedFfmpegPath = FindFfmpegInKnownLocations() ?? "ffmpeg";
            return _cachedFfmpegPath;
        }
    }

    public string ResolveFfprobe()
    {
        var ffmpeg = ResolveFfmpeg();
        if (Path.IsPathRooted(ffmpeg) && File.Exists(ffmpeg))
        {
            var directory = Path.GetDirectoryName(ffmpeg)!;
            var extension = Path.GetExtension(ffmpeg);
            var candidate = Path.Combine(directory, "ffprobe" + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        if (string.Equals(ffmpeg, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";

        return DeriveFfprobeFrom(ffmpeg);
    }

    public bool IsFfmpegAvailable()
    {
        var path = ResolveFfmpeg();
        return !Path.IsPathRooted(path) || File.Exists(path);
    }

    internal static string DeriveFfprobeFrom(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath)
            || string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "ffprobe";
        }

        var directory = Path.GetDirectoryName(ffmpegPath);
        var extension = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(directory)
            ? "ffprobe" + extension
            : Path.Combine(directory, "ffprobe" + extension);
    }

    private string? FindFfmpegInKnownLocations()
    {
        var localAppData = ResolveFolder(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = ResolveFolder(Environment.SpecialFolder.ProgramFiles);
        var userProfile = ResolveFolder(Environment.SpecialFolder.UserProfile);

        var wingetDirectory = Path.Combine(
            localAppData,
            "Microsoft",
            "WinGet",
            "Packages");
        if (Directory.Exists(wingetDirectory))
        {
            try
            {
                var ffmpegDirectories = Directory.GetDirectories(
                    wingetDirectory,
                    "Gyan.FFmpeg*");
                foreach (var directory in ffmpegDirectories)
                {
                    var binDirectories = SafeFileEnumeration
                        .EnumerateDirectoriesSafe(directory)
                        .Where(sub => string.Equals(
                            Path.GetFileName(sub),
                            "bin",
                            StringComparison.OrdinalIgnoreCase))
                        .OrderBy(sub => sub, StringComparer.OrdinalIgnoreCase);
                    foreach (var bin in binDirectories)
                    {
                        var candidate = Path.Combine(bin, "ffmpeg.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }
            catch
            {
                // Nicht lesbare Installationsordner werden wie bisher übersprungen.
            }
        }

        var chocolatey = Path.Combine(
            ResolveFolder(Environment.SpecialFolder.CommonApplicationData),
            "chocolatey",
            "bin",
            "ffmpeg.exe");
        if (File.Exists(chocolatey))
            return chocolatey;

        var scoop = Path.Combine(userProfile, "scoop", "shims", "ffmpeg.exe");
        if (File.Exists(scoop))
            return scoop;

        if (File.Exists(_manualFfmpegPath))
            return _manualFfmpegPath;

        var programFilesPath = Path.Combine(
            programFiles,
            "ffmpeg",
            "bin",
            "ffmpeg.exe");
        return File.Exists(programFilesPath) ? programFilesPath : null;
    }

    private string ResolveFolder(Environment.SpecialFolder folder)
        => _specialFolderPathResolver(folder) ?? string.Empty;
}
