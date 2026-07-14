using AuswertungPro.Next.Application.Map;

namespace AuswertungPro.Next.Infrastructure.Map;

public sealed class KatasterXtfFilePathResolver : IKatasterXtfPathResolver
{
    private static readonly string[] PreferredFileNames =
    [
        "Abwasserkataster_Uri_korrigiert.xtf",
        "Abwasserkataster_Uri.xtf"
    ];

    public string Resolve(string? explicitPath, string? directoryPath)
    {
        var normalizedExplicitPath = Normalize(explicitPath);
        if (!string.IsNullOrWhiteSpace(normalizedExplicitPath))
        {
            if (File.Exists(normalizedExplicitPath))
                return normalizedExplicitPath;

            if (Directory.Exists(normalizedExplicitPath)
                && TryFindKatasterXtf(normalizedExplicitPath) is { } fromExplicitDirectory)
            {
                return fromExplicitDirectory;
            }
        }

        var normalizedDirectoryPath = Normalize(directoryPath);
        if (!string.IsNullOrWhiteSpace(normalizedDirectoryPath)
            && Directory.Exists(normalizedDirectoryPath)
            && TryFindKatasterXtf(normalizedDirectoryPath) is { } fromDirectory)
        {
            return fromDirectory;
        }

        return normalizedExplicitPath ?? string.Empty;
    }

    public string? TryFindKatasterXtf(string? directoryPath)
    {
        var normalizedDirectoryPath = Normalize(directoryPath);
        if (string.IsNullOrWhiteSpace(normalizedDirectoryPath) || !Directory.Exists(normalizedDirectoryPath))
            return null;

        foreach (var fileName in PreferredFileNames)
        {
            var candidate = Path.Combine(normalizedDirectoryPath, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            return Directory.EnumerateFiles(normalizedDirectoryPath, "*.xtf", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.Length)
                .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .Select(info => info.FullName)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
