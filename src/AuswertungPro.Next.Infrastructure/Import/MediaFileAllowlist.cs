using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Zentrale Allowlist fuer Dateien, die aus Fremd-Importen (XTF/WinCan/IBAK) als Medien
/// aufgeloest oder in den Projektordner kopiert werden duerfen (Security-Befunde S2-1..S2-3).
/// Verhindert, dass eine praeparierte Importdatei beliebige lokale Dateien (Dokumente,
/// ausfuehrbare Dateien) referenziert und ueber die Projektweitergabe exfiltriert.
/// Absolute Pfade auf ANDEREN Laufwerken bleiben erlaubt (WinCan-Workflows) — beschraenkt
/// wird nur der Dateityp, nicht der Ort.
/// </summary>
internal static class MediaFileAllowlist
{
    // Zusaetzlich zu den kanonischen Listen in MediaFileTypes: Transport-Stream-Container
    // aus WinCan/IBAK-Exporten (vgl. BackupExclusionRules, TrainingCenterImportService).
    private static readonly string[] ExtraVideoExtensions = [".ts", ".mts", ".m2ts", ".m4v"];

    /// <summary>Bekannte Video- oder Bild-Endung (die Medientypen der App).</summary>
    internal static bool IsMediaFile(string? path)
        => MediaFileTypes.HasVideoExtension(path)
           || MediaFileTypes.HasImageExtension(path)
           || HasExtraVideoExtension(path);

    internal static bool IsPdf(string? path)
        => string.Equals(Path.GetExtension((path ?? string.Empty).Trim()), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Medien oder Protokoll-PDF. Fuer Link-/PDF-Felder, in denen neben Videos/Fotos auch
    /// Original-Protokolle (PDF) ins Projekt kopiert werden.
    /// </summary>
    internal static bool IsImportableMediaOrPdf(string? path)
        => IsMediaFile(path) || IsPdf(path);

    /// <summary>
    /// UNC-Pfade (\\host\share bzw. //host/share) aus Fremd-Importen ablehnen:
    /// ein Zugriff loest SMB-Authentifizierung aus und gibt NTLM-Hashes an fremde Hosts preis.
    /// </summary>
    internal static bool IsUnc(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return trimmed.StartsWith(@"\\", StringComparison.Ordinal)
               || trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool HasExtraVideoExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path.Trim());
        return !string.IsNullOrWhiteSpace(ext)
               && ExtraVideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }
}
