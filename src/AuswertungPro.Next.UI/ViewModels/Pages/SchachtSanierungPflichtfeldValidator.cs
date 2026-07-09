using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

internal static class SchachtSanierungPflichtfeldValidator
{
    private static readonly string[] SanierenAliases =
    {
        "Sanieren_JaNein",
        "Sanieren Ja/Nein",
        "Sanieren ja/nein",
        "Ja/Nein",
        "Sanieren"
    };

    private static readonly string[] AusgefuehrtDurchAliases =
    {
        "Ausgefuehrt_durch",
        "Ausgefuehrt durch",
        "Ausgeführt durch",
        "Ausgefuhrt durch",
        "Sanieren durch",
        "Sanierung durch"
    };

    public static IReadOnlyList<string> MissingFields(SchachtRecord? record)
    {
        if (record is null)
            return Array.Empty<string>();

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ResolveValue(record, SanierenAliases)))
            missing.Add("Sanieren Ja/Nein");
        if (string.IsNullOrWhiteSpace(ResolveValue(record, AusgefuehrtDurchAliases)))
            missing.Add("Ausgefuehrt durch");

        return missing;
    }

    private static string ResolveValue(SchachtRecord record, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var value = record.GetFieldValue(alias);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        var normalizedAliases = aliases
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var pair in record.Fields)
        {
            if (normalizedAliases.Contains(Normalize(pair.Key)) && !string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value;
        }

        return string.Empty;
    }

    private static string Normalize(string value)
        => (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal);
}
