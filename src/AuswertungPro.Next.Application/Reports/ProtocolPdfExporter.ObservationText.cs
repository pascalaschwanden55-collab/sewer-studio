using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// Observation-Text-Builder fuer das Protokoll-PDF: Range-, Detail-, Meter-,
// Zeit-, Mpeg-, Stufe-, Notes- und Parameter-Texte. Pure Formatter ohne
// Layout-Verantwortung. Slice 1f.
public sealed partial class ProtocolPdfExporter
{
    private static string BuildRangeText(ProtocolEntry entry, string rangeLabel)
    {
        if (entry.MeterStart is null && entry.MeterEnd is null)
            return $"{rangeLabel} -";

        var m1 = FmtMeterValue(entry.MeterStart);
        var m2 = FmtMeterValue(entry.MeterEnd);
        return $"{rangeLabel} {m1}-{m2} m";
    }

    private static string BuildDetailLine(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add("Zeit " + FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildObservationMeterText(ProtocolEntry entry)
    {
        var start = entry.MeterStart;
        var end = entry.MeterEnd;

        if (entry.IsStreckenschaden && start.HasValue && end.HasValue)
            return $"{FmtMeterValue(start)}–{FmtMeterValue(end)}";

        if (start.HasValue)
            return FmtMeterValue(start);

        if (end.HasValue)
            return FmtMeterValue(end);

        return "-";
    }

    private static string BuildObservationTimeText(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add(FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    private static string BuildObservationMeterStartText(ProtocolEntry entry)
    {
        var value = entry.MeterStart ?? entry.MeterEnd;
        return value.HasValue ? FmtMeterValue(value) : "-";
    }

    private static string BuildObservationMpegText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            return entry.Mpeg.Trim();
        if (entry.Zeit.HasValue)
            return FormatTime(entry.Zeit.Value);
        return "-";
    }

    private static string BuildObservationStufeText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Severity))
            return entry.CodeMeta.Severity!.Trim();
        if (entry.CodeMeta?.Count is not null)
            return entry.CodeMeta.Count.Value.ToString(CultureInfo.InvariantCulture);
        return "-";
    }

    private static string BuildObservationNotesText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is not null)
        {
            var remark = GetParam(parameters, "vsa.anmerkung");
            if (!string.IsNullOrWhiteSpace(remark))
                return Shorten(remark.Trim(), 60);
        }

        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Notes))
            return Shorten(entry.CodeMeta.Notes.Trim(), 60);

        return "-";
    }

    private static string BuildParameterShortText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var list = new List<string>();
        foreach (var kv in parameters.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            if (kv.Key.StartsWith("vsa.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung1", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung2", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add($"{kv.Key}={kv.Value}");
        }

        if (list.Count == 0)
        {
            var q1 = GetParam(parameters, "Quantifizierung1") ?? GetParam(parameters, "vsa.q1");
            var q2 = GetParam(parameters, "Quantifizierung2") ?? GetParam(parameters, "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                list.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                list.Add($"Q2={q2}");
        }

        return string.Join(", ", list);
    }
}
