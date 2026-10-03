using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Protokolltext einer Haltung -> Beobachtungen und JSON-Protokoll der Verteilung (aus
/// <see cref="TrainingCenterImportService"/> verschoben, Paket A 03.10.2026, ohne Verhaltensaenderung).
/// Das JSON ist mit <c>PdfProtocolExtractor.ExtractFromJson</c> kompatibel und wird atomar geschrieben.
/// </summary>
internal static class TrainingCenterProtokollJson
{
    /// <summary>
    /// Extrahiert Beobachtungen aus dem Chunk-Text (Fretz-Format + Standard).
    /// Unbekannte Codes werden am Parse-Eintritt verworfen, damit PDF-Freitext
    /// nicht als Trainingslabel in den Batch gelangt.
    /// </summary>
    internal static List<TrainingCenterImportService.ProtocolEntry> ExtractEntriesFromChunkText(string text)
    {
        var entries = new List<TrainingCenterImportService.ProtocolEntry>();
        if (string.IsNullOrWhiteSpace(text))
            return entries;

        // Fretz-Format: "[Foto?] [HH:MM:SS] [Meter] [Code] [Beschreibung]"
        var fretzRx = new Regex(
            @"^\s*(?:\d{1,5}\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?<meter>\d{1,4}[.,]\d{1,3})\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)\s+(?<text>.+?)(?:\s{2,}|$)",
            RegexOptions.Multiline);

        foreach (Match m in fretzRx.Matches(text))
        {
            var code = m.Groups["code"].Value.Trim();
            if (!VsaCodeValidator.IsKnownCode(code))
                continue;

            var desc = m.Groups["text"].Value.Trim();
            if (double.TryParse(m.Groups["meter"].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var meter))
            {
                entries.Add(new TrainingCenterImportService.ProtocolEntry(code.Replace(".", "").ToUpperInvariant(), desc, meter));
            }
        }

        return entries;
    }

    internal static void WriteProtocolJson(
        string path,
        List<TrainingCenterImportService.ProtocolEntry> entries,
        string haltungId,
        string pageRange)
    {
        // Format kompatibel mit PdfProtocolExtractor.ExtractFromJson:
        // { "Current": { "Entries": [ { "Code": "BCD", "Beschreibung": "...", "MeterStart": 0.0 } ] } }
        var jsonEntries = entries.Select(e => new Dictionary<string, object>
        {
            ["Code"] = e.Code,
            ["Beschreibung"] = e.Beschreibung,
            ["MeterStart"] = e.MeterStart,
            ["MeterEnd"] = e.MeterStart,
            ["IsStreckenschaden"] = false,
            ["IsDeleted"] = false
        }).ToArray();

        var root = new Dictionary<string, object>
        {
            ["HaltungId"] = haltungId,
            ["PageRange"] = pageRange,
            ["Current"] = new Dictionary<string, object>
            {
                ["Entries"] = jsonEntries
            }
        };

        var json = JsonSerializer.Serialize(root, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json);
    }
}
