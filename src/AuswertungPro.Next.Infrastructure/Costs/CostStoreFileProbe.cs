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
                    "Verknüpfte Katalogdateien sind nicht erlaubt.");
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

    /// <summary>
    /// Entfernt eine Benutzer-Override-Datei sicher: Eine fehlende Datei gilt als entfernt, ein
    /// verknuepfter Pfad, ein Ordner am Dateipfad oder eine nicht loeschbare Datei wird gemeldet.
    /// Gemeinsam fuer Kostenkatalog und Massnahmenvorlagen.
    /// </summary>
    public static bool TryRemove(string path, out string error)
    {
        error = "";
        try
        {
            var probe = Probe(path);
            if (probe.State == CostStorePathState.Invalid)
            {
                error = probe.Error ?? "User-Override ist nicht sicher zugreifbar.";
                return false;
            }

            if (probe.State == CostStorePathState.File)
                File.Delete(path);

            if (Probe(path).State != CostStorePathState.Missing)
            {
                error = "User-Override konnte nicht sicher entfernt werden.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ShouldUseProjectCandidate(string path)
        => Probe(path).State != CostStorePathState.Missing;
}
