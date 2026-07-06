using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Protocol;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfObservationText;

namespace AuswertungPro.Next.Application.Reports;

internal static class ProtocolPdfEntryResolver
{
    internal static double? ResolveHoldingLength(HaltungRecord record, IReadOnlyList<ProtocolEntry> entries)
    {
        var raw = record.GetFieldValue("Haltungslaenge_m");
        var parsed = TryParseDouble(raw);
        if (parsed.HasValue && parsed.Value > 0)
            return parsed.Value;

        var max = entries
            .Select(e => e.MeterEnd ?? e.MeterStart)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .DefaultIfEmpty(0d)
            .Max();

        return max > 0 ? max : null;
    }

    internal static List<ProtocolEntry> ResolveEntriesForExport(HaltungRecord record, ProtocolDocument doc)
    {
        var current = doc.Current?.Entries ?? new List<ProtocolEntry>();
        var active = current.Where(e => !e.IsDeleted).ToList();
        var fromFindings = BuildImportedEntriesFromFindings(record.VsaFindings);
        RepairExistingImportedEntries(active, fromFindings);

        var deletedKeys = new HashSet<string>(current.Where(e => e.IsDeleted).Select(BuildEntryKey));
        var existingKeys = new HashSet<string>(active.Select(BuildEntryKey));

        var keyToEntry = new Dictionary<string, ProtocolEntry>();
        foreach (var entry in active)
        {
            var key = BuildEntryKey(entry);
            keyToEntry.TryAdd(key, entry);
        }

        var result = new List<ProtocolEntry>(active);

        foreach (var entry in fromFindings)
        {
            var key = BuildEntryKey(entry);
            if (deletedKeys.Contains(key))
                continue;

            if (existingKeys.Contains(key))
            {
                if (keyToEntry.TryGetValue(key, out var existing))
                    ProtocolPhotoPathMerger.MergePhotoPaths(existing, entry);
                continue;
            }

            result.Add(entry);
            existingKeys.Add(key);
            keyToEntry.TryAdd(key, entry);
        }

        if (fromFindings.Count == 0)
        {
            var fromPrimary = ParsePrimaryDamagesToEntries(record.GetFieldValue("Primaere_Schaeden"));
            foreach (var entry in fromPrimary)
            {
                var key = BuildEntryKey(entry);
                if (deletedKeys.Contains(key) || existingKeys.Contains(key))
                    continue;

                result.Add(entry);
                existingKeys.Add(key);
            }
        }

        // Redundante Fortsetzungs-/Quantifizierungszeilen zu einer Beobachtung falten
        // (Merge statt Drop, kein Datenverlust) -> Protokoll wie das Original schlank halten.
        return ObservationCollapser.Collapse(result);
    }

    private static void RepairExistingImportedEntries(
        IReadOnlyList<ProtocolEntry> active,
        IReadOnlyList<ProtocolEntry> importedFromFindings)
    {
        if (active.Count == 0 || importedFromFindings.Count == 0)
            return;

        foreach (var existing in active)
        {
            var imported = importedFromFindings.FirstOrDefault(f => LooksLikeSameObservation(existing, f));
            if (imported is null)
                continue;

            if (existing.MeterEnd.HasValue && imported.MeterEnd is null)
            {
                existing.MeterEnd = null;
                existing.IsStreckenschaden = false;
            }

            if (existing.MeterStart is null && imported.MeterStart.HasValue)
                existing.MeterStart = imported.MeterStart;

            MergeCodeMeta(existing, imported);
            ProtocolPhotoPathMerger.MergePhotoPaths(existing, imported);
        }
    }

    private static bool LooksLikeSameObservation(ProtocolEntry left, ProtocolEntry right)
    {
        if (!string.Equals(left.Code?.Trim(), right.Code?.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        var leftMeter = left.MeterStart ?? left.MeterEnd;
        var rightMeter = right.MeterStart ?? right.MeterEnd;
        if (leftMeter.HasValue && rightMeter.HasValue && Math.Abs(leftMeter.Value - rightMeter.Value) > 0.01)
            return false;

        var leftDesc = NormalizeKeyText(left.Beschreibung ?? string.Empty);
        var rightDesc = NormalizeKeyText(right.Beschreibung ?? string.Empty);
        return string.IsNullOrWhiteSpace(leftDesc)
               || string.IsNullOrWhiteSpace(rightDesc)
               || string.Equals(leftDesc, rightDesc, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeCodeMeta(ProtocolEntry target, ProtocolEntry source)
    {
        if (source.CodeMeta?.Parameters is null || source.CodeMeta.Parameters.Count == 0)
            return;

        target.CodeMeta ??= new ProtocolEntryCodeMeta { Code = target.Code };
        target.CodeMeta.Parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source.CodeMeta.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                target.CodeMeta.Parameters[kv.Key] = kv.Value;
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

            var mStart = f.MeterStart;
            var mEnd = f.MeterEnd;
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

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1)
                || !string.IsNullOrWhiteSpace(f.Quantifizierung2)
                || TryFormatClock(f.SchadenlageAnfang) is not null
                || TryFormatClock(f.SchadenlageEnde) is not null)
            {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                    ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                };

                var uhrVon = TryFormatClock(f.SchadenlageAnfang);
                var uhrBis = TryFormatClock(f.SchadenlageEnde);
                if (!string.IsNullOrWhiteSpace(uhrVon))
                    parameters["vsa.uhr.von"] = uhrVon;
                if (!string.IsNullOrWhiteSpace(uhrBis))
                    parameters["vsa.uhr.bis"] = uhrBis;

                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = parameters,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static string? TryFormatClock(double? value)
        => value is > 0 and <= 12
            ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
            : null;

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

            list.Add(new ProtocolEntry
            {
                Code = code,
                Beschreibung = desc ?? string.Empty,
                MeterStart = meter,
                IsStreckenschaden = false,
                Source = ProtocolEntrySource.Imported
            });
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
        return Regex.Replace(text.Trim(), @"\s+", " ");
    }
}
