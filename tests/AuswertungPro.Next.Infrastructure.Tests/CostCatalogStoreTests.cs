using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CostCatalogStoreTests
{
    [Fact]
    public void PreserveNpkMetadata_FillsFromDefault_WhenOverrideEmpty()
    {
        // Alter Preis-Override ohne NPK-Metadaten (vor der NPK-Erweiterung gespeichert).
        var def = new CostCatalogItem { Key = "X", NpkCode = "612.110", Chapter = "600" };
        var ovr = new CostCatalogItem { Key = "X", NpkCode = "", Chapter = "", Price = 99m };

        var merged = CostCatalogStore.PreserveNpkMetadata(ovr, def);

        Assert.Equal("612.110", merged.NpkCode); // aus Default aufgefüllt
        Assert.Equal("600", merged.Chapter);
        Assert.Equal(99m, merged.Price);         // Preis aus Override bleibt
    }

    [Fact]
    public void PreserveNpkMetadata_KeepsOverride_WhenPresent()
    {
        var def = new CostCatalogItem { Key = "X", NpkCode = "612.110", Chapter = "600" };
        var ovr = new CostCatalogItem { Key = "X", NpkCode = "999.999", Chapter = "900" };

        var merged = CostCatalogStore.PreserveNpkMetadata(ovr, def);

        Assert.Equal("999.999", merged.NpkCode); // Override gewinnt, wenn vorhanden
        Assert.Equal("900", merged.Chapter);
    }

    [Fact]
    public void BuildUserOverridesForSave_BlanksDefaultNpkMetadata_WhenUnchanged()
    {
        var defaults = new CostCatalog
        {
            Items =
            {
                new CostCatalogItem { Key = "X", Name = "Position X", NpkCode = "612.110", Chapter = "600", Price = 10m }
            }
        };
        var edited = new CostCatalog
        {
            Version = 1,
            Currency = "CHF",
            VatRate = 0.081m,
            Items =
            {
                new CostCatalogItem { Key = "X", Name = "Position X", NpkCode = "612.110", Chapter = "600", Price = 99m }
            }
        };

        var toSave = CostCatalogStore.BuildUserOverridesForSave(edited, defaults);

        var item = Assert.Single(toSave.Items);
        Assert.Equal("", item.NpkCode);
        Assert.Equal("", item.Chapter);
        Assert.Equal(99m, item.Price);
    }

    [Fact]
    public void BuildUserOverridesForSave_KeepsChangedNpkMetadata()
    {
        var defaults = new CostCatalog
        {
            Items =
            {
                new CostCatalogItem { Key = "X", Name = "Position X", NpkCode = "612.110", Chapter = "600" }
            }
        };
        var edited = new CostCatalog
        {
            Items =
            {
                new CostCatalogItem { Key = "X", Name = "Position X", NpkCode = "612.111", Chapter = "600" }
            }
        };

        var toSave = CostCatalogStore.BuildUserOverridesForSave(edited, defaults);

        var item = Assert.Single(toSave.Items);
        Assert.Equal("612.111", item.NpkCode);
        Assert.Equal("", item.Chapter);
    }
}
