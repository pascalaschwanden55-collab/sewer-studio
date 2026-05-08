using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// AI-Summary-Block des Protokoll-PDF: KI-Vorschlag-Hinweis pro Eintrag
// + zusammenfassender Kopfblock (akzeptiert/abgelehnt/offen, Top-Codes,
// haeufigste Flags). Reflection-Helper greifen auf eine "Ai"-Property
// am ProtocolEntry zu — typed-Migration spaeter (Slice 6 oder eigener
// Domain-Cleanup).
public sealed partial class ProtocolPdfExporter
{
    private static string BuildAiSummary(List<ProtocolEntry> entries, ProtocolPdfExportOptions options)
    {
        var aiEntries = entries.Select(e => GetMember(e, "Ai")).Where(ai => ai != null).ToList();
        if (aiEntries.Count == 0)
            return "Keine KI-Daten vorhanden.";

        var accepted = aiEntries.Count(ai => GetBool(ai, "Accepted"));
        var rejected = aiEntries.Count(ai => GetBool(ai, "Rejected"));
        var undecided = aiEntries.Count - accepted - rejected;

        var topCodes = aiEntries
            .Select(ai => SafeString(GetMember(ai, "FinalCode")) ?? SafeString(GetMember(ai, "SuggestedCode")))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .GroupBy(c => c!)
            .OrderByDescending(g => g.Count())
            .Take(Math.Max(1, options.MaxAiSummaryCodes))
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var allFlags = aiEntries
            .SelectMany(ai => AsStringEnumerable(GetMember(ai, "Flags")))
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .GroupBy(f => f)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var parts = new List<string>
        {
            $"Akzeptiert: {accepted}   Abgelehnt: {rejected}   Offen: {undecided}"
        };
        if (topCodes.Count > 0)
            parts.Add("Top KI-Codes: " + string.Join(", ", topCodes));
        if (allFlags.Count > 0)
            parts.Add("Häufigste KI-Flags: " + string.Join(", ", allFlags));

        return string.Join("    ", parts);
    }

    private static void ComposeAiHintBlock(ColumnDescriptor block, ProtocolEntry e, ProtocolPdfExportOptions options)
    {
        var ai = GetMember(e, "Ai");
        if (ai == null)
            return;

        var accepted = GetBool(ai, "Accepted");
        var rejected = GetBool(ai, "Rejected");

        if (options.ShowAiHintsOnlyIfDecided && !(accepted || rejected))
            return;

        var status = accepted ? "übernommen" : rejected ? "abgelehnt" : "offen";
        var code = SafeString(GetMember(ai, "FinalCode")) ?? SafeString(GetMember(ai, "SuggestedCode")) ?? "—";
        var conf = SafeDouble(GetMember(ai, "Confidence"))?.ToString("0.00") ?? "—";
        var reason = SafeString(GetMember(ai, "Reason")) ?? SafeString(GetMember(ai, "ReasonShort")) ?? "";
        var flags = AsStringEnumerable(GetMember(ai, "Flags")).ToList();
        var flagsText = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";

        block.Item().Text($"KI-Vorschlag: {code} ({conf}) – {status}{flagsText}").FontSize(9).Italic();
        if (!string.IsNullOrWhiteSpace(reason))
            block.Item().Text($"Grund: {reason}").FontSize(9).Italic();
    }

    private static object? GetMember(object? obj, string name)
    {
        if (obj == null) return null;
        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(obj);
    }

    private static bool GetBool(object? obj, string name)
    {
        var v = GetMember(obj, name);
        return v is bool b && b;
    }

    private static double? SafeDouble(object? v)
        => v is double d ? d : v is float f ? f : v is decimal m ? (double)m : null;

    private static string? SafeString(object? v) => v as string;

    private static IEnumerable<string> AsStringEnumerable(object? v)
    {
        if (v is IEnumerable<string> es) return es;
        if (v is IEnumerable<object> eo) return eo.Select(x => x?.ToString() ?? "");
        return Array.Empty<string>();
    }

    private static string JoinFlags(object? flags)
    {
        var list = AsStringEnumerable(flags).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return list.Count == 0 ? "" : string.Join(", ", list);
    }
}
