using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageStartFilter(string FieldName, string Value)
{
    public static DataPageStartFilter FromDashboardZustand(string key)
        => new("Zustandsklasse", NormalizeZustandKey(key));

    public static DataPageStartFilter FromDashboardSchaden(string key)
        => new("Primaere_Schaeden", NormalizeDamageGroup(key));

    public static DataPageStartFilter FromDashboardDn(string key)
        => new("DN_mm", NormalizeDnKey(key));

    public bool Matches(HaltungRecord? record)
    {
        if (record is null)
            return false;

        return FieldName switch
        {
            "Zustandsklasse" => MatchesZustand(record),
            "DN_mm" => string.Equals(NormalizeDnKey(record.GetFieldValue("DN_mm")), Value, StringComparison.OrdinalIgnoreCase),
            "Primaere_Schaeden" => EnumerateDamageGroups(record).Any(c => string.Equals(c, Value, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private bool MatchesZustand(HaltungRecord record)
    {
        var recordKey = DashboardStatisticsBuilder.NormalizeZustandsklasse(record.GetFieldValue("Zustandsklasse"));
        return string.Equals(recordKey, Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeZustandKey(string? key)
    {
        var text = (key ?? string.Empty).Trim();
        if (text.StartsWith("Z", StringComparison.OrdinalIgnoreCase))
            text = text[1..];

        return DashboardStatisticsBuilder.NormalizeZustandsklasse(text);
    }

    private static string NormalizeDnKey(string? key)
    {
        var digits = new string((key ?? string.Empty).Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? "?" : digits;
    }

    private static IEnumerable<string> EnumerateDamageGroups(HaltungRecord record)
    {
        foreach (var entry in record.Protocol?.Current?.Entries ?? Enumerable.Empty<ProtocolEntry>())
        {
            if (!entry.IsDeleted)
                yield return NormalizeDamageGroup(entry.Code);
        }

        if (record.ProtocolEntry is { IsDeleted: false } legacy)
            yield return NormalizeDamageGroup(legacy.Code);

        foreach (var finding in record.VsaFindings)
            yield return NormalizeDamageGroup(finding.KanalSchadencode);

        foreach (var token in EnumeratePrimaryDamageTokens(record.GetFieldValue("Primaere_Schaeden")))
            yield return NormalizeDamageGroup(token);
    }

    private static IEnumerable<string> EnumeratePrimaryDamageTokens(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (Match match in Regex.Matches(raw, "[A-Za-z0-9]+"))
            yield return match.Value;
    }

    private static string NormalizeDamageGroup(string? code)
    {
        var text = new string((code ?? string.Empty).Trim().ToUpperInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
        if (text.Length == 0)
            return string.Empty;

        return text.Length <= 3 ? text : text[..3];
    }
}
