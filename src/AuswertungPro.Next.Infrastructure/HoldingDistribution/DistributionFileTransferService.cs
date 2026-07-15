using System.Globalization;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Fuehrt die Dateiuebertragung der manuellen Verteilung aus und verhindert
/// unbeabsichtigtes Ueberschreiben durch fortlaufende Zieldateinamen.
/// </summary>
public sealed class DistributionFileTransferService : IDistributionFileTransfer
{
    public string EnsureUniquePath(string path, bool overwrite)
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

    public void MoveOrCopy(string source, string destination, bool move, bool overwrite)
    {
        if (move)
            File.Move(source, destination, overwrite);
        else
            File.Copy(source, destination, overwrite);
    }
}
