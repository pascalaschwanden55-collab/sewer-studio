using System.Collections.Generic;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class CostCatalogDnPriceEditorTests
{
    [Fact]
    public void CreateNextRow_bei_leerer_Liste_DN100_Preis0()
    {
        var row = CostCatalogDnPriceEditor.CreateNextRow(new List<DnPrice>());
        Assert.Equal(100, row.DnFrom);
        Assert.Equal(100, row.DnTo);
        Assert.Equal(0m, row.Price);
    }

    [Fact]
    public void CreateNextRow_schlaegt_naechste_DN_ueber_dem_groessten_vor()
    {
        var existing = new List<DnPrice>
        {
            new() { DnFrom = 250, DnTo = 250, Price = 200m },
            new() { DnFrom = 300, DnTo = 300, Price = 220m },
        };
        var row = CostCatalogDnPriceEditor.CreateNextRow(existing);
        Assert.Equal(350, row.DnFrom);
        Assert.Equal(350, row.DnTo);
        Assert.Equal(0m, row.Price);
    }
}
