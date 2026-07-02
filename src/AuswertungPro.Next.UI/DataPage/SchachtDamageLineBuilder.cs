using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record SchachtDamageLine(string Component, string Text, string DisplayText);

public static class SchachtDamageLineBuilder
{
    private static readonly string[] DamageFieldCandidates =
    [
        "Prim\u00e4re Sch\u00e4den",
        "Primaere Schaeden",
        "Primaere_Schaeden",
        "Prim\u00c3\u00a4re Sch\u00c3\u00a4den"
    ];

    public static IReadOnlyList<SchachtDamageLine> Build(SchachtRecord record)
    {
        var raw = ResolveDamageText(record);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<SchachtDamageLine>();

        return raw.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) || !string.IsNullOrWhiteSpace(line.Component))
            .ToList();
    }

    public static string ResolveDamageText(SchachtRecord record)
    {
        foreach (var field in DamageFieldCandidates)
        {
            var value = record.GetFieldValue(field);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static SchachtDamageLine ParseLine(string raw)
    {
        var line = (raw ?? string.Empty).Trim();
        if (line.Length == 0)
            return new SchachtDamageLine("", "", "");

        var colon = line.IndexOf(':');
        if (colon > 0 && colon < line.Length - 1)
        {
            var component = line[..colon].Trim();
            var text = line[(colon + 1)..].Trim();
            return new SchachtDamageLine(component, text, $"{component}: {text}");
        }

        return new SchachtDamageLine("", line, line);
    }
}
