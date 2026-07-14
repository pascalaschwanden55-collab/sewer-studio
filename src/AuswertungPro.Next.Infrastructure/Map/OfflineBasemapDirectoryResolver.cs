using AuswertungPro.Next.Application.Map;

namespace AuswertungPro.Next.Infrastructure.Map;

public sealed class OfflineBasemapDirectoryResolver : IOfflineBasemapPathResolver
{
    private const string SatellitSubfolder = "satellit";
    private const string AvSubfolder = "av";

    public string? Resolve(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        if (HasMapFolder(configuredPath))
            return configuredPath;

        var parent = Path.GetDirectoryName(
            configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(parent) && HasMapFolder(parent))
            return parent;

        return configuredPath;
    }

    private static bool HasMapFolder(string basePath)
        => Directory.Exists(Path.Combine(basePath, SatellitSubfolder))
           || Directory.Exists(Path.Combine(basePath, AvSubfolder));
}
