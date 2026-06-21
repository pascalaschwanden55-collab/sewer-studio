using System.Globalization;
using System.Reflection;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

internal static class ProtocolPdfValueFormatting
{
    internal static string? GetMeta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v : null;

    internal static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    internal static List<(string Label, string? Value)> FilterNonEmpty(List<(string Label, string? Value)> items)
        => items.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToList();

    internal static double MapToLine(double value, double length, double left, double right)
    {
        if (length <= 0)
            return left;

        var t = Math.Clamp(value / length, 0d, 1d);
        return left + (right - left) * t;
    }

    internal static string Svg(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static object? GetMember(object? obj, string name)
    {
        if (obj == null)
            return null;

        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(obj);
    }

    internal static bool GetBool(object? obj, string name)
    {
        var v = GetMember(obj, name);
        return v is bool b && b;
    }

    internal static double? SafeDouble(object? v)
        => v is double d ? d : v is float f ? f : v is decimal m ? (double)m : null;

    internal static string? SafeString(object? v) => v as string;

    internal static IEnumerable<string> AsStringEnumerable(object? v)
    {
        if (v is IEnumerable<string> es)
            return es;
        if (v is IEnumerable<object> eo)
            return eo.Select(x => x?.ToString() ?? "");

        return Array.Empty<string>();
    }

    internal static string JoinFlags(object? flags)
    {
        var list = AsStringEnumerable(flags).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return list.Count == 0 ? "" : string.Join(", ", list);
    }

    internal static string EscapeCsv(string s)
    {
        if (s.Contains('"') || s.Contains(';') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    internal static string FmtMeter(double? m) => m is null ? "\u2014" : m.Value.ToString("0.00");

    internal static string BuildParameterText(ProtocolEntry e)
    {
        var parts = new List<string>();
        if (e.CodeMeta?.Parameters != null && e.CodeMeta.Parameters.Count > 0)
        {
            var p = e.CodeMeta.Parameters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}");
            parts.Add("Parameter: " + string.Join(", ", p));
        }
        if (!string.IsNullOrWhiteSpace(e.CodeMeta?.Severity))
            parts.Add($"Severity: {e.CodeMeta.Severity}");
        if (e.CodeMeta?.Count is not null)
            parts.Add($"Count: {e.CodeMeta.Count}");
        if (!string.IsNullOrWhiteSpace(e.CodeMeta?.Notes))
            parts.Add($"Notes: {e.CodeMeta.Notes}");

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
