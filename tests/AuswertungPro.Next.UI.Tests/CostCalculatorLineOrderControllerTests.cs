using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorLineOrderControllerTests
{
    [Fact]
    public void OrderLines_sortiert_nach_gruppe_template_reihenfolge_text_itemkey_und_erhaelt_stabile_reihenfolge()
    {
        var unknownA = Line("Unbekannt", "Z", "Zulu");
        var sameFirst = Line("Vorarbeiten", "A", "Alpha");
        var sameSecond = Line("Vorarbeiten", "A", "Alpha");
        var templateLater = Line("Vorarbeiten", "B", "Beta");
        var installation = Line("Installation", "I", "Installation");

        var ordered = CostCalculatorLineOrderController.OrderLines(
            new[] { unknownA, templateLater, sameFirst, installation, sameSecond },
            itemKey => string.Equals(itemKey, "A", StringComparison.OrdinalIgnoreCase) ? 0 : 10);

        Assert.Equal(new[] { installation, sameFirst, sameSecond, templateLater, unknownA }, ordered);
    }

    [Fact]
    public void OrderTemplateLines_sortiert_nach_gruppe_und_erhaelt_originalreihenfolge_innerhalb_gruppe()
    {
        var unknown = TemplateLine("Sonstiges", "S");
        var first = TemplateLine("Vorarbeiten", "A");
        var second = TemplateLine("Vorarbeiten", "B");
        var installation = TemplateLine("Installation", "I");

        var ordered = CostCalculatorLineOrderController.OrderTemplateLines(
            new[] { unknown, first, second, installation });

        Assert.Equal(new[] { installation, first, second, unknown }, ordered);
    }

    private static CostLineVm Line(string group, string itemKey, string text)
        => new()
        {
            Group = group,
            ItemKey = itemKey,
            Text = text
        };

    private static MeasureLineTemplate TemplateLine(string group, string itemKey)
        => new()
        {
            Group = group,
            ItemKey = itemKey
        };
}
