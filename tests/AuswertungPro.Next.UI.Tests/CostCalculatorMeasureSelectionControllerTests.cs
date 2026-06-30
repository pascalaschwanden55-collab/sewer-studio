using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureSelectionControllerTests
{
    [Fact]
    public void SetSelectedMeasures_ersetzt_auswahl_und_ignoriert_deaktivierte_vorlagen()
    {
        var controller = new CostCalculatorMeasureSelectionController();

        controller.SetSelectedMeasures(new[]
        {
            TemplateListItem("Alt", "Alt", disabled: false)
        });

        controller.SetSelectedMeasures(new[]
        {
            TemplateListItem("A", "Aktiv", disabled: false),
            TemplateListItem("X", "Deaktiviert", disabled: true),
            TemplateListItem("B", "Zweit", disabled: false)
        });

        Assert.Equal(new[] { "A", "B" }, controller.SelectedMeasureIds);
    }

    [Fact]
    public void OrderMeasures_sortiert_nach_template_reihenfolge_name_und_stabiler_originalreihenfolge()
    {
        var controller = new CostCalculatorMeasureSelectionController();
        controller.ReplaceMeasureOrder(new[] { "B", "A" });
        var unknownFirst = Block("Z", "Alpha");
        var templateB = Block("B", "Zulu");
        var templateA = Block("A", "Beta");
        var unknownSecond = Block("Y", "Alpha");

        var ordered = controller.OrderMeasures(new[] { unknownFirst, unknownSecond, templateA, templateB });

        Assert.Equal(new[] { templateB, templateA, unknownFirst, unknownSecond }, ordered);
    }

    private static MeasureTemplateListItem TemplateListItem(string id, string name, bool disabled)
        => new(new MeasureTemplate
        {
            Id = id,
            Name = name,
            Disabled = disabled
        });

    private static MeasureBlockVm Block(string id, string name)
        => new(
            new MeasureTemplate
            {
                Id = id,
                Name = name
            },
            new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase));
}
