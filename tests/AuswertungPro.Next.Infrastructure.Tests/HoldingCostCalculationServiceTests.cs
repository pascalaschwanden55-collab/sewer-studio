using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingCostCalculationServiceTests
{
    [Fact]
    public void Calculate_AppliesDnLengthConnectionsAndTotals()
    {
        var service = new HoldingCostCalculationService();

        var result = service.Calculate(
            Templates(),
            Catalog(),
            new HoldingCostCalculationRequest
            {
                Holding = "100-200",
                MeasureIds = ["SCHLAUCHLINER_NADELFILZ"],
                Dn = 200,
                LengthMeters = 10.5m,
                Connections = 2,
                VatRate = 0.081m
            });

        Assert.Empty(result.Warnings);
        Assert.Equal("100-200", result.Cost.Holding);
        Assert.Equal(5135.00m, result.Cost.Total);
        Assert.Equal(415.94m, result.Cost.MwstAmount);
        Assert.Equal(5550.94m, result.Cost.TotalInclMwst);

        var measure = Assert.Single(result.Cost.Measures);
        Assert.Equal(200, measure.Dn);
        Assert.Equal(10.5m, measure.LengthMeters);

        var liner = measure.Lines.Single(l => l.ItemKey == "SCHLAUCHLINER_NADELFILZ");
        Assert.Equal(10.5m, liner.Qty);
        Assert.Equal(270m, liner.UnitPrice);

        var connection = measure.Lines.Single(l => l.ItemKey == "ANSCHLUSS_EINBINDEN");
        Assert.True(connection.Selected);
        Assert.Equal(2m, connection.Qty);
        Assert.Equal(900m, connection.UnitPrice);
    }

    [Fact]
    public void Calculate_DisablesConnectionLinesWhenConnectionsAreZero()
    {
        var service = new HoldingCostCalculationService();

        var result = service.Calculate(
            Templates(),
            Catalog(),
            new HoldingCostCalculationRequest
            {
                Holding = "100-200",
                MeasureIds = ["SCHLAUCHLINER_NADELFILZ"],
                Dn = 200,
                LengthMeters = 10m,
                Connections = 0
            });

        var connection = result.Cost.Measures
            .Single()
            .Lines
            .Single(l => l.ItemKey == "ANSCHLUSS_EINBINDEN");

        Assert.False(connection.Selected);
        Assert.Equal(0m, connection.Qty);
        Assert.Equal(3200.00m, result.Cost.Total);
    }

    [Fact]
    public void Calculate_UsesNearestDnBucketAndWarns_WhenExactDnIsMissing()
    {
        var service = new HoldingCostCalculationService();

        var result = service.Calculate(
            Templates(),
            Catalog(),
            new HoldingCostCalculationRequest
            {
                Holding = "100-200",
                MeasureIds = ["SCHLAUCHLINER_NADELFILZ"],
                Dn = 225,
                LengthMeters = 10m,
                Connections = 0
            });

        var liner = result.Cost.Measures
            .Single()
            .Lines
            .Single(l => l.ItemKey == "SCHLAUCHLINER_NADELFILZ");

        Assert.Equal(270m, liner.UnitPrice);
        Assert.Contains(result.Warnings, w => w.Contains("Kein exakter DN-Preis"));
    }

    [Fact]
    public void Calculate_WarnsInsteadOfThrowing_WhenMeasureIsUnknown()
    {
        var service = new HoldingCostCalculationService();

        var result = service.Calculate(
            Templates(),
            Catalog(),
            new HoldingCostCalculationRequest
            {
                Holding = "100-200",
                MeasureIds = ["NICHT_DA"],
                Dn = 200,
                LengthMeters = 10m
            });

        Assert.Empty(result.Cost.Measures);
        Assert.Equal(0m, result.Cost.Total);
        Assert.Contains(result.Warnings, w => w.Contains("Massnahme nicht gefunden"));
    }

    [Fact]
    public void CalculateProject_SplitsInstallationAcrossSelectedHoldings()
    {
        var service = new HoldingCostCalculationService();

        var result = service.CalculateProject(
            ProjectTemplates(),
            ProjectCatalog(),
            new ProjectCostCalculationRequest
            {
                Holdings =
                [
                    new()
                    {
                        Holding = "H1",
                        MeasureIds = ["SCHLAUCHLINER_GFK"],
                        Dn = 300,
                        LengthMeters = 10m
                    },
                    new()
                    {
                        Holding = "H2",
                        MeasureIds = ["SCHLAUCHLINER_GFK"],
                        Dn = 300,
                        LengthMeters = 20m
                    }
                ],
                VatRate = 0.081m
            });

        Assert.Empty(result.Warnings);
        Assert.Equal(4300m, result.TotalCost.Total);

        Assert.Equal(1600m, result.Store.ByHolding["H1"].Total);
        Assert.Equal(2700m, result.Store.ByHolding["H2"].Total);

        var h1Install = result.Store.ByHolding["H1"].Measures
            .Single()
            .Lines
            .Single(l => l.ItemKey == "INSTALL_UV_ANLAGE");
        Assert.Equal(0.5m, h1Install.Qty);
        Assert.Equal(1000m, h1Install.UnitPrice);
        Assert.Equal("100.1", h1Install.SubmissionPos);

        var installSummary = result.SummaryLines.Single(l => l.Label == "Installation UV-Anlage");
        Assert.Equal("ProjectSplit", installSummary.AllocationScope);
        Assert.Equal(1m, installSummary.Qty);
        Assert.Equal(1000m, installSummary.Amount);
        Assert.Equal(["H1", "H2"], installSummary.Holdings);

        var linerSummary = result.SummaryLines.Single(l => l.Label == "Schlauchliner (GFK)");
        Assert.Equal("600.5", linerSummary.SubmissionPos);
        Assert.Equal(30m, linerSummary.Qty);
        Assert.Equal(3000m, linerSummary.Amount);

        var prelinerSummary = result.SummaryLines.Single(l => l.Label == "Preliner / Schutzfolie");
        Assert.Equal("600.1", prelinerSummary.SubmissionPos);
        Assert.Equal("PerHolding", prelinerSummary.AllocationScope);
        Assert.Equal(30m, prelinerSummary.Qty);
        Assert.Equal(300m, prelinerSummary.Amount);
    }

    private static CostCatalog Catalog()
        => new()
        {
            Currency = "CHF",
            VatRate = 0.081m,
            Items = new List<CostCatalogItem>
            {
                new()
                {
                    Key = "INSTALL_HL_ANLAGE",
                    Name = "Installation HL-Anlage",
                    Unit = "pl",
                    Type = "Fixed",
                    Price = 500m
                },
                new()
                {
                    Key = "SCHLAUCHLINER_NADELFILZ",
                    Name = "Schlauchliner (Nadelfilz)",
                    Unit = "m",
                    Type = "ByDN",
                    DnPrices = new List<DnPrice>
                    {
                        new() { DnFrom = 150, DnTo = 150, Price = 250m },
                        new() { DnFrom = 200, DnTo = 200, Price = 270m },
                        new() { DnFrom = 250, DnTo = 250, Price = 300m }
                    }
                },
                new()
                {
                    Key = "ANSCHLUSS_EINBINDEN",
                    Name = "Anschlusseinbinden",
                    Unit = "stk",
                    Type = "Fixed",
                    Price = 900m
                }
            }
        };

    private static CostCatalog ProjectCatalog()
        => new()
        {
            Currency = "CHF",
            VatRate = 0.081m,
            Items = new List<CostCatalogItem>
            {
                new()
                {
                    Key = "INSTALL_UV_ANLAGE",
                    Name = "Installation UV-Anlage",
                    Unit = "pl",
                    Type = "Fixed",
                    Price = 1000m
                },
                new()
                {
                    Key = "SCHLAUCHLINER_GFK",
                    Name = "Schlauchliner (GFK)",
                    Unit = "m",
                    Type = "ByDN",
                    DnPrices = new List<DnPrice>
                    {
                        new() { DnFrom = 300, DnTo = 300, Price = 100m }
                    }
                },
                new()
                {
                    Key = "SCHLAUCHLINER_PRELINER",
                    Name = "Preliner / Schutzfolie",
                    Unit = "m",
                    Type = "ByDN",
                    DnPrices = new List<DnPrice>
                    {
                        new() { DnFrom = 300, DnTo = 300, Price = 10m }
                    }
                }
            }
        };

    private static MeasureTemplateCatalog Templates()
        => new()
        {
            Measures = new List<MeasureTemplate>
            {
                new()
                {
                    Id = "SCHLAUCHLINER_NADELFILZ",
                    Name = "Schlauchliner (Nadelfilz)",
                    Lines = new List<MeasureLineTemplate>
                    {
                        new() { Group = "Installation", ItemKey = "INSTALL_HL_ANLAGE", Enabled = true, DefaultQty = 1m },
                        new() { Group = "Hauptarbeit", ItemKey = "SCHLAUCHLINER_NADELFILZ", Enabled = true, DefaultQty = 1m },
                        new() { Group = "Hauptarbeit", ItemKey = "ANSCHLUSS_EINBINDEN", Enabled = true, DefaultQty = 1m }
                    }
                }
            }
        };

    private static MeasureTemplateCatalog ProjectTemplates()
        => new()
        {
            Measures = new List<MeasureTemplate>
            {
                new()
                {
                    Id = "SCHLAUCHLINER_GFK",
                    Name = "Schlauchliner (GFK)",
                    Lines = new List<MeasureLineTemplate>
                    {
                        new() { Group = "Installation", ItemKey = "INSTALL_UV_ANLAGE", Enabled = true, DefaultQty = 1m },
                        new() { Group = "Hauptarbeit", ItemKey = "SCHLAUCHLINER_PRELINER", Enabled = true, DefaultQty = 1m },
                        new() { Group = "Hauptarbeit", ItemKey = "SCHLAUCHLINER_GFK", Enabled = true, DefaultQty = 1m }
                    }
                }
            }
        };
}
