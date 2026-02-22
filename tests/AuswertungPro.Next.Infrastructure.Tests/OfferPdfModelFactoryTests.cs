using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class OfferPdfModelFactoryTests
{
    [Fact]
    public void CreateCostSummary_AggregatesMeasureOwnerAndPositionTotals()
    {
        var entries = new List<CostSummaryEntry>
        {
            new()
            {
                Holding = "H-1",
                Owner = "Privat",
                Cost = new HoldingCost
                {
                    Holding = "H-1",
                    Total = 100m,
                    MwstRate = 0.081m,
                    MwstAmount = 8.10m,
                    TotalInclMwst = 108.10m,
                    Measures = new List<MeasureCost>
                    {
                        new()
                        {
                            MeasureId = "M1",
                            MeasureName = "Schlauchliner (GFK)",
                            Lines = new List<CostLine>
                            {
                                new()
                                {
                                    Group = "Hauptarbeit",
                                    ItemKey = "SCHLAUCHLINER_GFK",
                                    Text = "Schlauchliner (GFK)",
                                    Unit = "m",
                                    Qty = 1m,
                                    UnitPrice = 100m,
                                    Selected = true
                                }
                            }
                        }
                    }
                }
            },
            new()
            {
                Holding = "H-2",
                Owner = "AWU",
                Cost = new HoldingCost
                {
                    Holding = "H-2",
                    Total = 200m,
                    MwstRate = 0.081m,
                    MwstAmount = 16.20m,
                    TotalInclMwst = 216.20m,
                    Measures = new List<MeasureCost>
                    {
                        new()
                        {
                            MeasureId = "M1",
                            MeasureName = "Schlauchliner (GFK)",
                            Lines = new List<CostLine>
                            {
                                new()
                                {
                                    Group = "Hauptarbeit",
                                    ItemKey = "SCHLAUCHLINER_GFK",
                                    Text = "Schlauchliner (GFK)",
                                    Unit = "m",
                                    Qty = 2m,
                                    UnitPrice = 50m,
                                    Selected = true
                                }
                            }
                        },
                        new()
                        {
                            MeasureId = "M2",
                            MeasureName = "Anschluss einbinden",
                            Lines = new List<CostLine>
                            {
                                new()
                                {
                                    Group = "Hauptarbeit",
                                    ItemKey = "ANSCHLUSS_EINBINDEN",
                                    Text = "Anschluss einbinden",
                                    Unit = "stk",
                                    Qty = 1m,
                                    UnitPrice = 100m,
                                    Selected = true
                                }
                            }
                        }
                    }
                }
            }
        };

        var ctx = new OfferPdfContext
        {
            ProjectTitle = "Projekt A",
            VariantTitle = "Kosten",
            Currency = "CHF"
        };

        var model = OfferPdfModelFactory.CreateCostSummary(entries, ctx, DateTimeOffset.Parse("2026-02-08T10:00:00Z"));

        Assert.Equal("Kostenzusammenstellung", model.DocumentKindLabel);
        Assert.Equal("300.00 CHF", model.Totals.NetText);
        Assert.Contains("24.30 CHF", model.Totals.VatText);
        Assert.Equal("324.30 CHF", model.Totals.GrossText);

        Assert.Equal(2, model.MeasureSummaryLines.Count);
        Assert.Contains(model.MeasureSummaryLines, x => x.MeasureName == "Schlauchliner (GFK)" && x.NetText == "200.00 CHF");
        Assert.Contains(model.MeasureSummaryLines, x => x.MeasureName == "Anschluss einbinden" && x.NetText == "100.00 CHF");

        Assert.Equal(2, model.OwnerSummaryLines.Count);
        Assert.Contains(model.OwnerSummaryLines, x => x.Owner == "Privat" && x.NetText == "100.00 CHF");
        Assert.Contains(model.OwnerSummaryLines, x => x.Owner == "AWU" && x.NetText == "200.00 CHF");

        Assert.Equal(2, model.PositionSummaryLines.Count);
        var liner = model.PositionSummaryLines.Single(x => x.Position == "Schlauchliner (GFK)");
        Assert.Equal("3", liner.QtyText);
        Assert.Equal("200.00 CHF", liner.TotalText);
        Assert.Equal("variabel", liner.UnitPriceText);

        Assert.Equal(4, model.SpecialStatsLines.Count);
        var gfk = model.SpecialStatsLines.Single(x => x.Category == "Inliner GFK");
        Assert.Equal("3", gfk.QtyText);
        Assert.Equal("m", gfk.Unit);
        Assert.Equal("200.00 CHF", gfk.NetText);

        var nadelfilz = model.SpecialStatsLines.Single(x => x.Category == "Inliner Nadelfilz");
        Assert.Equal("0", nadelfilz.QtyText);

        var manschette = model.SpecialStatsLines.Single(x => x.Category == "Manschetten");
        Assert.Equal("0", manschette.QtyText);

        var lem = model.SpecialStatsLines.Single(x => x.Category == "Linerendmanschetten (LEM)");
        Assert.Equal("0", lem.QtyText);
    }

    [Fact]
    public void CreateCostSummary_CanDisableOwnerAndPositionSections()
    {
        var entries = new List<CostSummaryEntry>
        {
            new()
            {
                Holding = "H-1",
                Owner = "Privat",
                Cost = new HoldingCost
                {
                    Holding = "H-1",
                    Total = 80m,
                    MwstRate = 0.081m,
                    MwstAmount = 6.48m,
                    TotalInclMwst = 86.48m,
                    Measures = new List<MeasureCost>
                    {
                        new()
                        {
                            MeasureId = "M1",
                            MeasureName = "Kurzliner",
                            Lines = new List<CostLine>
                            {
                                new()
                                {
                                    Group = "Hauptarbeit",
                                    ItemKey = "KURZLINER_PER_ST",
                                    Text = "Kurzliner",
                                    Unit = "stk",
                                    Qty = 1m,
                                    UnitPrice = 80m,
                                    Selected = true
                                }
                            }
                        }
                    }
                }
            }
        };

        var model = OfferPdfModelFactory.CreateCostSummary(
            entries,
            new OfferPdfContext { Currency = "CHF" },
            DateTimeOffset.Parse("2026-02-08T10:00:00Z"),
            includeOwnerSummary: false,
            includePositionSummary: false);

        Assert.Single(model.MeasureSummaryLines);
        Assert.Equal(4, model.SpecialStatsLines.Count);
        Assert.Empty(model.OwnerSummaryLines);
        Assert.Empty(model.PositionSummaryLines);
    }
}
