using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// ProtocolPdfExporter Eintragsbau aus Importen: Erzeugt ProtocolEntries aus
// VsaFindings (XTF-Import) bzw. aus Primaere_Schaeden-Freitext, mergt
// Foto-Pfade und baut Dedup-Schluessel zur Vermeidung von Doppeln.
// Aus dem Hauptdatei extrahiert (Slice 21b).
public sealed partial class ProtocolPdfExporter
{
    private static void MergePhotoPaths(ProtocolEntry target, ProtocolEntry source)
    {
        if (source.FotoPaths is null || source.FotoPaths.Count == 0)
            return;

        var existing = new HashSet<string>(
            target.FotoPaths.Select(p => p.Replace('\\', '/').ToUpperInvariant()));

        foreach (var path in source.FotoPaths)
        {
            var normalized = path.Replace('\\', '/').ToUpperInvariant();
            if (existing.Add(normalized))
                target.FotoPaths.Add(path);
        }
    }

    private static List<ProtocolEntry> BuildImportedEntriesFromFindings(IReadOnlyList<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>();
        if (findings is null || findings.Count == 0)
            return list;

        foreach (var f in findings)
        {
            if (string.IsNullOrWhiteSpace(f.KanalSchadencode))
                continue;

            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            if (mStart is null && !string.IsNullOrWhiteSpace(f.Raw))
                mStart = TryParseMeterFromRaw(f.Raw);
            if (mEnd is null && !string.IsNullOrWhiteSpace(f.Raw))
                mEnd = TryParseSecondMeterFromRaw(f.Raw);

            var time = ParseMpegTime(f.MPEG)
                       ?? (f.Timestamp is null ? null : f.Timestamp.Value.TimeOfDay);
            if (time is null && !string.IsNullOrWhiteSpace(f.Raw))
            {
                var rawTime = TryParseTimeFromRaw(f.Raw);
                time = ParseMpegTime(rawTime);
            }

            var entry = new ProtocolEntry
            {
                Code = f.KanalSchadencode?.Trim() ?? string.Empty,
                Beschreibung = f.Raw?.Trim() ?? string.Empty,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static List<ProtocolEntry> ParsePrimaryDamagesToEntries(string? rawText)
    {
        var list = new List<ProtocolEntry>();
        if (string.IsNullOrWhiteSpace(rawText))
            return list;

        var lines = rawText.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = (line ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            if (trimmed.StartsWith("...", StringComparison.Ordinal))
                continue;

            if (!TryParsePrimaryDamageLine(trimmed, out var code, out var meter, out var desc))
                continue;

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = desc ?? string.Empty,
                MeterStart = meter,
                IsStreckenschaden = false,
                Source = ProtocolEntrySource.Imported
            };

            list.Add(entry);
        }

        return list;
    }

    private static bool TryParsePrimaryDamageLine(string line, out string code, out double? meter, out string? desc)
    {
        code = string.Empty;
        meter = null;
        desc = null;

        var match = Regex.Match(line, @"^\s*(?<code>[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?)\s*@\s*(?<m>\d+(?:[.,]\d+)?)\s*m?\s*(?:\((?<desc>.+)\))?\s*$");
        if (!match.Success)
            return false;

        code = match.Groups["code"].Value.Trim();
        var mText = match.Groups["m"].Value.Replace(',', '.');
        if (double.TryParse(mText, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            meter = val;
        desc = match.Groups["desc"].Success ? match.Groups["desc"].Value.Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(code);
    }

    private static string BuildEntryKey(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        var start = entry.MeterStart ?? entry.MeterEnd ?? -1;
        var end = entry.MeterEnd ?? entry.MeterStart ?? -1;
        var desc = NormalizeKeyText(entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "");
        return string.Format(CultureInfo.InvariantCulture, "{0}|{1:0.00}|{2:0.00}|{3}", code, start, end, desc);
    }

    private static string NormalizeKeyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        return normalized;
    }

    public static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
    {
        if (string.IsNullOrWhiteSpace(holdingLabel))
            return (null, null);

        var parts = holdingLabel
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
            return (parts[0], null);
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return (null, null);
    }
}
