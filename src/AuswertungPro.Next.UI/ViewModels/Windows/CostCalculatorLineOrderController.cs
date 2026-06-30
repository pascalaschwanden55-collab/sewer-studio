using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public static class CostCalculatorLineOrderController
{
    private static readonly string[] GroupOrder =
    {
        "Installation",
        "Vorarbeiten",
        "Hauptarbeit",
        "Qualitaetskontrolle",
        "Qualitaet",
        "Sonstiges"
    };

    public static IReadOnlyList<MeasureLineTemplate> OrderTemplateLines(
        IEnumerable<MeasureLineTemplate> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines
            .Select((line, index) => new
            {
                Line = line,
                Index = index,
                Order = GetGroupOrder(line.Group)
            })
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Index)
            .Select(x => x.Line)
            .ToList();
    }

    public static IReadOnlyList<CostLineVm> OrderLines(
        IEnumerable<CostLineVm> lines,
        Func<string?, int> getTemplateLineOrder)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(getTemplateLineOrder);

        return lines
            .Select((line, index) => new
            {
                Line = line,
                Index = index,
                GroupOrder = GetGroupOrder(line.Group),
                TemplateOrder = getTemplateLineOrder(line.ItemKey),
                Text = line.Text ?? string.Empty,
                ItemKey = line.ItemKey ?? string.Empty
            })
            .OrderBy(x => x.GroupOrder)
            .ThenBy(x => x.TemplateOrder)
            .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ItemKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Index)
            .Select(x => x.Line)
            .ToList();
    }

    private static int GetGroupOrder(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return GroupOrder.Length + 1;

        var trimmed = group.Trim();
        var idx = Array.FindIndex(GroupOrder, g => string.Equals(g, trimmed, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : GroupOrder.Length + 1;
    }
}
