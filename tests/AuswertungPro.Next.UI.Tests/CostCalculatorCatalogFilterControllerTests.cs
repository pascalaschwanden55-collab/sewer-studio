using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorCatalogFilterControllerTests
{
    [Fact]
    public void ReplaceItems_baut_aktive_optionen_sortiert_und_bevorzugt_template_gruppe()
    {
        var controller = new CostCalculatorCatalogFilterController();

        controller.ReplaceItems(
            new[]
            {
                CatalogItem("CUSTOM", "Kundenposition", "Stk"),
                CatalogItem("SCHLAUCHLINER_STD", "Schlauchliner", "m"),
                CatalogItem("INSTALL_SETUP", "Installation", "Stk"),
                CatalogItem("INACTIVE", "Inaktiv", "Stk", active: false)
            },
            new[]
            {
                Template("CUSTOM", "Vorarbeiten")
            },
            searchText: "");

        Assert.Equal(
            new[] { "INSTALL_SETUP", "CUSTOM", "SCHLAUCHLINER_STD" },
            controller.AllCatalogItems.Select(item => item.Key));
        Assert.Equal("Vorarbeiten", controller.AllCatalogItems.Single(item => item.Key == "CUSTOM").Group);
        Assert.Equal(controller.AllCatalogItems, controller.FilteredCatalogItems);
    }

    [Fact]
    public void ApplyFilter_filtert_case_insensitive_nach_displayname()
    {
        var controller = new CostCalculatorCatalogFilterController();
        controller.ReplaceItems(
            new[]
            {
                CatalogItem("INSTALL_SETUP", "Installation", "Stk"),
                CatalogItem("SCHLAUCHLINER_STD", "Schlauchliner", "m")
            },
            Array.Empty<MeasureTemplate>(),
            searchText: "");

        controller.ApplyFilter("liner");

        Assert.Equal(new[] { "SCHLAUCHLINER_STD" }, controller.FilteredCatalogItems.Select(item => item.Key));
    }

    private static CostCatalogItem CatalogItem(string key, string name, string unit, bool active = true)
        => new()
        {
            Key = key,
            Name = name,
            Unit = unit,
            Active = active
        };

    private static MeasureTemplate Template(string itemKey, string group)
        => new()
        {
            Id = "template",
            Name = "Template",
            Lines =
            [
                new MeasureLineTemplate
                {
                    ItemKey = itemKey,
                    Group = group
                }
            ]
        };
}
