using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CostCatalogStoreTests
{
    [Fact]
    public void SaveUserOverrides_IsBlocked_WhenExistingUserOverrideCouldNotBeLoaded()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");
        File.WriteAllText(overridePath, "{ kaputt");
        var store = new CostCatalogStore(overridePath);

        _ = store.LoadUserOverrides();
        var ok = store.SaveUserOverrides(new CostCatalog(), out var error);

        Assert.False(ok);
        Assert.Contains("konnte nicht geladen werden", error, StringComparison.OrdinalIgnoreCase);
    }

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

    [Fact]
    public void FindDuplicateNpkCodesWithDifferentUnits_Warnt_Nur_Bei_Unterschiedlichen_Einheiten()
    {
        var catalog = new CostCatalog
        {
            Items =
            {
                new CostCatalogItem { Key = "A", NpkCode = "311.111", Unit = "h", Active = true },
                new CostCatalogItem { Key = "B", NpkCode = "311.111", Unit = "m", Active = true },
                new CostCatalogItem { Key = "C", NpkCode = "612.111", Unit = "m", Active = true },
                new CostCatalogItem { Key = "D", NpkCode = "612.111", Unit = "m", Active = true },
                new CostCatalogItem { Key = "E", NpkCode = "999.999", Unit = "Stk", Active = false }
            }
        };

        var warning = Assert.Single(CostCatalogStore.FindDuplicateNpkCodesWithDifferentUnits(catalog));

        Assert.Equal("311.111", warning.NpkCode);
        Assert.Equal(new[] { "h", "m" }, warning.Units);
        Assert.Equal(new[] { "A", "B" }, warning.ItemKeys);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cost_catalog_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
