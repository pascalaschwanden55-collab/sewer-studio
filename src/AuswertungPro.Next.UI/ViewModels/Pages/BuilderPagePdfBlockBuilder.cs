using System.Collections.Generic;
using System.Text;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class BuilderPagePdfBlockBuilder
{
    public static string BuildProjectCustomerBlock(IReadOnlyDictionary<string, string> metadata)
    {
        var sb = new StringBuilder();

        AddLine(sb, metadata, "Auftraggeber");
        AddLine(sb, metadata, "FirmaName");
        AddLine(sb, metadata, "FirmaAdresse");
        AddLine(sb, metadata, "FirmaTelefon");
        AddLine(sb, metadata, "FirmaEmail");

        var result = sb.ToString().Trim();
        return result.Length == 0 ? "Nicht definiert" : result;
    }

    public static string BuildObjectBlock(IReadOnlyDictionary<string, string> metadata, int holdingCount)
    {
        var lines = new List<string>();
        AddLine(lines, "Projekt", metadata.TryGetValue("Zone", out var zone) ? zone : "");
        AddLine(lines, "Gemeinde", metadata.TryGetValue("Gemeinde", out var gemeinde) ? gemeinde : "");
        AddLine(lines, "Auftrag-Nr.", metadata.TryGetValue("AuftragNr", out var auftragNr) ? auftragNr : "");
        AddLine(lines, "Bearbeiter", metadata.TryGetValue("Bearbeiter", out var bearbeiter) ? bearbeiter : "");
        AddLine(lines, "Inspektionsdatum", metadata.TryGetValue("InspektionsDatum", out var datum) ? datum : "");
        lines.Add($"Haltungen im Ausdruck: {holdingCount}");
        return string.Join("\n", lines);
    }

    private static void AddLine(StringBuilder sb, IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
        {
            return;
        }

        value = SafeText(value);
        if (value.Length == 0)
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append('\n');
        }

        sb.Append(value);
    }

    private static void AddLine(List<string> lines, string label, string value)
    {
        value = SafeText(value);
        if (value.Length == 0)
        {
            return;
        }

        lines.Add($"{label}: {value}");
    }

    private static string SafeText(string? value)
        => (value ?? "").Trim();
}
