using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageProtocolObservationSync(
    string PrimaryDamageText,
    List<VsaFinding> Findings);

public static class DataPageProtocolObservationMapper
{
    private static readonly Regex ContinuousDefectMarkerRegex = new(@"^[AB]\d{2}$", RegexOptions.Compiled);
    private static readonly Regex EmbeddedVsaCodeRegex = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    public static DataPageProtocolObservationSync Build(
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyList<VsaFinding>? existingFindings)
    {
        var primaryLines = BuildPrimaryDamageLines(entries);
        var primaryText = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", primaryLines));
        var findings = BuildFindings(entries, existingFindings);

        return new DataPageProtocolObservationSync(primaryText, findings);
    }

    public static List<string> BuildPrimaryDamageLines(IEnumerable<ProtocolEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            var key = $"{code.ToUpperInvariant()}|{meterKey}";
            if (!seen.Add(key))
                continue;

            var parts = new List<string>();
            if (meter.HasValue)
                parts.Add($"{meter.Value:0.00}m");

            parts.Add(code);

            var description = NormalizeInlineText(entry.Beschreibung);
            if (!string.IsNullOrWhiteSpace(description))
                parts.Add(description);

            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                parts.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                parts.Add($"Q2={q2}");

            lines.Add(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        return lines;
    }

    public static List<VsaFinding> BuildFindings(
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyList<VsaFinding>? existingFindings)
    {
        var existing = existingFindings ?? Array.Empty<VsaFinding>();
        var list = new List<VsaFinding>(entries.Count);

        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var meterStart = entry.MeterStart;
            var meterEnd = entry.MeterEnd;
            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            var photo = entry.FotoPaths?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            var template = existing.FirstOrDefault(f =>
                AreCodesCompatible(code, f.KanalSchadencode)
                && AreMetersClose(meterStart, f.MeterStart ?? f.SchadenlageAnfang, 0.15));

            var finding = new VsaFinding
            {
                KanalSchadencode = code,
                Raw = (entry.Beschreibung ?? string.Empty).Trim(),
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                SchadenlageAnfang = meterStart,
                SchadenlageEnde = meterEnd,
                Quantifizierung1 = q1,
                Quantifizierung2 = q2,
                MPEG = string.IsNullOrWhiteSpace(entry.Mpeg) ? template?.MPEG : entry.Mpeg,
                FotoPath = string.IsNullOrWhiteSpace(photo) ? template?.FotoPath : photo,
                EZD = template?.EZD,
                EZS = template?.EZS,
                EZB = template?.EZB
            };

            if (entry.Zeit.HasValue)
                finding.Timestamp = DateTime.Today.Add(entry.Zeit.Value);
            else
                finding.Timestamp = template?.Timestamp;

            if (entry.IsStreckenschaden
                && meterStart.HasValue
                && meterEnd.HasValue
                && meterEnd.Value >= meterStart.Value)
            {
                finding.LL = meterEnd.Value - meterStart.Value;
            }
            else
            {
                finding.LL = template?.LL;
            }

            list.Add(finding);
        }

        return list;
    }

    public static bool HasFindingChanges(IReadOnlyList<VsaFinding>? oldFindings, IReadOnlyList<VsaFinding> newFindings)
    {
        var oldList = oldFindings ?? Array.Empty<VsaFinding>();
        if (oldList.Count != newFindings.Count)
            return true;

        for (var i = 0; i < oldList.Count; i++)
        {
            if (!string.Equals(BuildFindingFingerprint(oldList[i]), BuildFindingFingerprint(newFindings[i]), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string ResolveFindingEffectiveCode(string? code, string? rawDescription)
    {
        var normalizedCode = NormalizeCodeToken(code);
        if (!ContinuousDefectMarkerRegex.IsMatch(normalizedCode) || string.IsNullOrWhiteSpace(rawDescription))
            return normalizedCode;

        var text = rawDescription.Trim();
        if (text.StartsWith("(", StringComparison.Ordinal))
            text = text.Substring(1).TrimStart();

        var match = EmbeddedVsaCodeRegex.Match(text);
        return match.Success ? NormalizeCodeToken(match.Groups[1].Value) : normalizedCode;
    }

    private static string BuildFindingFingerprint(VsaFinding finding)
    {
        return string.Join("|",
            NormalizeCodeToken(finding.KanalSchadencode),
            FormatNullableDouble(finding.MeterStart),
            FormatNullableDouble(finding.MeterEnd),
            FormatNullableDouble(finding.SchadenlageAnfang),
            FormatNullableDouble(finding.SchadenlageEnde),
            finding.Raw?.Trim() ?? string.Empty,
            finding.Quantifizierung1?.Trim() ?? string.Empty,
            finding.Quantifizierung2?.Trim() ?? string.Empty,
            finding.MPEG?.Trim() ?? string.Empty,
            finding.FotoPath?.Trim() ?? string.Empty);
    }

    private static string NormalizeCodeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, "[^A-Z0-9]", string.Empty);
    }

    private static bool AreCodesCompatible(string? left, string? right)
    {
        var a = NormalizeCodeToken(left);
        var b = NormalizeCodeToken(right);
        if (a.Length == 0 || b.Length == 0)
            return false;

        return string.Equals(a, b, StringComparison.Ordinal)
               || a.StartsWith(b, StringComparison.Ordinal)
               || b.StartsWith(a, StringComparison.Ordinal);
    }

    private static bool AreMetersClose(double? left, double? right, double tolerance)
    {
        if (!left.HasValue || !right.HasValue)
            return false;

        return Math.Abs(left.Value - right.Value) <= tolerance;
    }

    private static string NormalizeInlineText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var oneLine = string.Join(" ",
            value.Replace("\r\n", "\n")
                 .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                 .Select(s => s.Trim())
                 .Where(s => s.Length > 0));

        return string.Join(" ", oneLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? GetCodeMetaParameter(ProtocolEntry entry, params string[] keys)
    {
        if (entry.CodeMeta?.Parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (entry.CodeMeta.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string FormatNullableDouble(double? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
}
