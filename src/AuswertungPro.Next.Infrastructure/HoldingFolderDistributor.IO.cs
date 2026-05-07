using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// File-System- und Marker-File-Helpers fuer HoldingFolderDistributor.
///
/// Refactor 2026-05-07 (Etappe 1, Charge R3): I/O-Helpers (Move/Copy,
/// EnsureUniquePath, BuildMissingInfo/AmbiguousInfo,
/// CopyCandidatesToUnmatched, FindExistingVideo) ausgegliedert.
/// Keine Verhaltensaenderung — alles statische Helfer in der gleichen
/// partial class.
/// </summary>
public static partial class HoldingFolderDistributor
{
    /// <summary>
    /// Kopiert alle Kandidaten-Videos in den __UNMATCHED-Sub-Folder bei
    /// Ambiguous-Match. Filename-Pattern: {date}_{haltung}_CANDIDATE_NN.{ext}.
    /// </summary>
    private static void CopyCandidatesToUnmatched(string unmatchedFolder, string dateStamp, string haltung, IReadOnlyList<string> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var src = candidates[i];
            var ext = Path.GetExtension(src);
            var name = $"{dateStamp}_{haltung}_CANDIDATE_{(i + 1).ToString("00", CultureInfo.InvariantCulture)}{ext}";
            var dest = EnsureUniquePath(Path.Combine(unmatchedFolder, name), overwrite: false);
            File.Copy(src, dest, overwrite: false);
        }
    }

    /// <summary>Inhalt der VIDEO_MISSING.txt-Marker-Datei.</summary>
    private static string BuildMissingInfo(string pdfPath, string videoName, DateTime date, string haltung)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VIDEO MISSING");
        sb.AppendLine($"PDF: {pdfPath}");
        sb.AppendLine($"Film: {videoName}");
        sb.AppendLine($"Datum: {date:dd.MM.yyyy}");
        sb.AppendLine($"Haltung: {haltung}");
        return sb.ToString();
    }

    /// <summary>Inhalt der VIDEO_AMBIGUOUS.txt-Marker-Datei mit Kandidaten-Liste.</summary>
    private static string BuildAmbiguousInfo(string pdfPath, string videoName, DateTime date, string haltung, IReadOnlyList<string> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VIDEO AMBIGUOUS");
        sb.AppendLine($"PDF: {pdfPath}");
        sb.AppendLine($"Film: {videoName}");
        sb.AppendLine($"Datum: {date:dd.MM.yyyy}");
        sb.AppendLine($"Haltung: {haltung}");
        sb.AppendLine("Candidates:");
        foreach (var c in candidates)
            sb.AppendLine($"- {c}");
        return sb.ToString();
    }

    /// <summary>File.Move oder File.Copy — abhaengig vom move-Flag.</summary>
    private static void MoveOrCopy(string source, string dest, bool move)
    {
        if (move)
        {
            File.Move(source, dest);
        }
        else
        {
            File.Copy(source, dest, overwrite: false);
        }
    }

    /// <summary>
    /// Sucht im Holding-Folder nach einem bereits vorhandenen Video, das dem
    /// Source-Video entspricht (Dateiname-Match oder gleiche Groesse).
    /// </summary>
    private static string? FindExistingVideo(string holdingFolder, string sourceVideoPath)
    {
        if (!Directory.Exists(holdingFolder) || !File.Exists(sourceVideoPath))
            return null;

        var srcInfo = new FileInfo(sourceVideoPath);
        var srcName = Path.GetFileName(sourceVideoPath);

        try
        {
            foreach (var existing in Directory.EnumerateFiles(holdingFolder))
            {
                if (!MediaFileTypes.HasVideoExtension(existing))
                    continue;

                // Exakter Dateiname-Match
                if (string.Equals(Path.GetFileName(existing), srcName, StringComparison.OrdinalIgnoreCase))
                    return existing;

                // Gleiche Dateigroesse = selbes Video (anderer Name)
                var existInfo = new FileInfo(existing);
                if (existInfo.Length == srcInfo.Length && existInfo.Length > 0)
                    return existing;
            }
        }
        catch
        {
            // Ordner nicht lesbar
        }

        return null;
    }

    /// <summary>
    /// Liefert path zurueck wenn overwrite=true oder Datei nicht existiert,
    /// sonst path mit numerischem Suffix (_01, _02, ..., _999).
    /// Schutz gegen versehentliches Ueberschreiben bei overwrite=false.
    /// </summary>
    private static string EnsureUniquePath(string path, bool overwrite)
    {
        if (overwrite || !File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i.ToString("00", CultureInfo.InvariantCulture)}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Unable to find free filename for {path}");
    }
}
