using System.IO;

namespace AuswertungPro.Next.Infrastructure.Costs;

internal enum CostStorePathState
{
    Missing,
    File,
    Invalid
}

internal sealed record CostStorePathProbeResult(
    CostStorePathState State,
    string? Error = null);

/// <summary>
/// Unterscheidet eine wirklich fehlende Katalogdatei von einem unlesbaren,
/// verknuepften oder als Ordner belegten Pfad. File.Exists allein ist dafuer
/// ungeeignet, weil es bei Zugriffsfehlern ebenfalls false liefert.
/// </summary>
internal static class CostStoreFileProbe
{
    public static CostStorePathProbeResult Probe(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return new CostStorePathProbeResult(
                    CostStorePathState.Invalid,
                    "Verknuepfte Katalogdateien sind nicht erlaubt.");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                return new CostStorePathProbeResult(
                    CostStorePathState.Invalid,
                    "Am erwarteten Dateipfad liegt ein Ordner.");
            }

            return new CostStorePathProbeResult(CostStorePathState.File);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new CostStorePathProbeResult(CostStorePathState.Missing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return new CostStorePathProbeResult(CostStorePathState.Invalid, ex.Message);
        }
    }

    public static bool ShouldUseProjectCandidate(string path)
        => Probe(path).State != CostStorePathState.Missing;
}
