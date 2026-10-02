namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Herkunftszeile ("# source=... bytes=... mtimeUtc=...") der Kataster-Tabellen von Haltungen und
/// Schaechten: Schreiben und Pruefen stehen zusammen, damit beide Tabellen dieselbe Frischeregel
/// haben (Deepscan 02.10.2026, B6).
/// </summary>
internal static class CadastreTableStamp
{
    /// <summary>Die erste Zeile der Tabelle fuer die gegebene XTF-Quelle.</summary>
    internal static string Line(string xtfPath, FileInfo sourceInfo)
        => $"# source={xtfPath}\tbytes={sourceInfo.Length}\tmtimeUtc={sourceInfo.LastWriteTimeUtc:O}";

    /// <summary>
    /// True, wenn die Tabelle existiert und ihre Herkunftszeile zu Groesse und Zeitstempel der
    /// XTF-Quelle passt. Fehlende Dateien, eine fehlende Herkunftszeile und Lesefehler gelten als nicht frisch.
    /// </summary>
    internal static bool IsFresh(string tablePath, string xtfPath)
    {
        if (!File.Exists(tablePath) || !File.Exists(xtfPath))
            return false;

        try
        {
            var firstLine = File.ReadLines(tablePath).FirstOrDefault();
            if (firstLine is null || !firstLine.StartsWith('#'))
                return false;

            var sourceInfo = new FileInfo(xtfPath);
            return firstLine.Contains($"bytes={sourceInfo.Length}", StringComparison.Ordinal)
                   && firstLine.Contains(
                       $"mtimeUtc={sourceInfo.LastWriteTimeUtc:O}",
                       StringComparison.Ordinal);
        }
        catch (Exception)
        {
            // Tabelle nicht lesbar (gesperrt, Zugriff verweigert, kaputte Datei): gilt als nicht frisch und wird neu gebaut.
            return false;
        }
    }
}
