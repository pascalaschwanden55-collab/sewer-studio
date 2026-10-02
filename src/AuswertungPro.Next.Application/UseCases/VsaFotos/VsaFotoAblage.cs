using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.VsaFotos;

/// <summary>
/// Bringt ein frisch aufgenommenes Befundfoto aus dem Temp-Ordner an seinen
/// dauerhaften Ort neben dem Video. Scheitert das, bleibt die Quelle liegen:
/// ein Foto im Temp-Ordner ist schlecht, ein geloeschtes Foto ist schlimmer.
/// </summary>
public static class VsaFotoAblage
{
    public static string Uebernehme(string quelle, string? videoPath, int photoIndex)
    {
        if (string.IsNullOrWhiteSpace(quelle))
            return quelle;

        try
        {
            var ziel = VsaFotoAblagePolicy.Ziel(
                videoPath,
                photoIndex,
                DateTimeOffset.Now,
                File.Exists);

            if (string.Equals(ziel, quelle, StringComparison.OrdinalIgnoreCase))
                return quelle;

            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.Move(quelle, ziel, overwrite: false);
            return ziel;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or NotSupportedException
                                       or ArgumentException
                                       or PathTooLongException)
        {
            // Den Grund fuer das Log festhalten; den sichtbaren Hinweis gibt der Aufnahmeweg
            // ueber VsaFotoTempHinweis (Deepscan 02.10.2026, R3).
            BestEffort.ReportWarning(
                $"[VsaFotoAblage] Foto bleibt im Temp-Ordner, Verschieben gescheitert: {ex.GetType().Name}: {ex.Message}");
            return quelle;
        }
    }
}
