using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Videoindex der Haltungsverteilung: normalisierte Haltungs-ID -> Videodatei (aus
/// <see cref="TrainingCenterImportService"/> verschoben, Paket A 03.10.2026, ohne Verhaltensaenderung).
/// Die sichere Ordnersuche betritt keine Verknuepfungen; ausgelassene Ordner und Videodateien werden gemeldet.
/// </summary>
internal static class TrainingCenterVideoIndex
{
    /// <summary>
    /// Regex zum Extrahieren einer Haltungs-ID aus einem Dateinamen.
    /// Erkennt z.B. "H_42046-41412.mpg" → "42046-41412"
    /// </summary>
    private static readonly Regex HaltungIdInFilename = new(
        @"(?<id>\d[\d\.]*[-/]\d[\d\.]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Erstellt einen Index: normalisierte Haltungs-ID → Videodatei-Pfad
    /// </summary>
    internal static Dictionary<string, string> BuildVideoIndex(
        string videoFolder,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            return index;

        var videoExts = new HashSet<string>(
            AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.VideoExtensions,
            StringComparer.OrdinalIgnoreCase)
        { ".ts", ".m4v" };

        // PR #85: ausgelassene Unterordner und Videodateien (Verknuepfung, nicht lesbar) werden genannt.
        var uebersprungeneOrdner = new List<string>();
        var uebersprungeneDateien = new List<string>();
        // Review PR #85: Ordner fuer Ordner, damit der Abbruch auch in einem Baum ohne passende Dateien
        // (leere Ordner, langsames Netzlaufwerk) wirkt; die Ordnersuche betritt keine Verknuepfungen.
        var dateien = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
            .EnumerateDirectoriesSafe(videoFolder, uebersprungeneOrdner)
            .SelectMany(ordner =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(
                    ordner, "*.*", recursive: false, uebersprungeneOrdner, uebersprungeneDateien);
            });
        foreach (var file in dateien)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Ausgeschlossene Videos (Grafik, Uebersicht) sind keine Inspektionsvideos; der Scan verwirft sie
            // ohnehin, die Verteilung darf sie deshalb auch nicht als Treffer zaehlen (Review PR #85).
            if (!videoExts.Contains(Path.GetExtension(file)) || TrainingCenterPaarung.IstAusgeschlossenesVideo(file))
                continue;

            var name = Path.GetFileNameWithoutExtension(file);
            var m = HaltungIdInFilename.Match(name);
            if (m.Success)
            {
                var id = NormalizeId(m.Groups["id"].Value);
                index.TryAdd(id, file);
            }
        }

        messages.AddRange(UebersprungeneOrdner.Meldungen(uebersprungeneOrdner));
        messages.AddRange(uebersprungeneDateien
            .Where(datei => videoExts.Contains(Path.GetExtension(datei)))
            .Select(datei => $"Video «{datei}» übersprungen: Verknüpfung oder nicht sicher prüfbar."));
        return index;
    }

    internal static string NormalizeId(string id)
        => (id ?? "").Trim().Replace(" ", "").Replace("/", "-");
}
