using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal static class DistributionFileTransfer
{
    public static string EnsureUniquePath(string path, bool overwrite)
    {
        if (overwrite || !File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(
                directory,
                $"{name}_{i.ToString("00", CultureInfo.InvariantCulture)}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Unable to find free filename for {path}");
    }

    public static void MoveOrCopy(string source, string destination, bool move, bool overwrite)
    {
        if (move)
            File.Move(source, destination, overwrite);
        else
            File.Copy(source, destination, overwrite);
    }
}
