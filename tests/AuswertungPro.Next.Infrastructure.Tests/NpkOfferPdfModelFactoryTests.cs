using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class NpkOfferPdfModelFactoryTests
{
    [Fact]
    public void Create_baut_Deckblatt_Kapitel_Positionen_und_Totalkette()
    {
        var positions = new List<AggregatedPosition>
        {
            new(
                NpkCode: "160.161.101",
                Chapter: "100",
                ItemKey: "INSTALL",
                Text: "Installation Kanalreinigungsfahrzeug",
                Unit: "pl",
                Dn: null,
                TotalQty: 1m,
                TotalNet: 1000m,
                HoldingCount: 2,
                IsVariablePrice: false,
                UnitPrice: 1000m),
            new(
                NpkCode: "612.110",
                Chapter: "600",
                ItemKey: "LINER",
                Text: "Schlauchliner",
                Unit: "m",
                Dn: 300,
                TotalQty: 10m,
                TotalNet: 2000m,
                HoldingCount: 1,
                IsVariablePrice: false,
                UnitPrice: 200m)
        };

        var model = NpkOfferPdfModelFactory.Create(
            positions,
            new NpkOfferPdfContext
            {
                ProjectTitle = "Offerte Sanierung",
                CustomerBlock = "Abwasser Uri",
                ObjectBlock = "Objekt: Test",
                ReferenceBlock = "Ihre Referenz: PA",
                VatRate = 0.081m,
                DiscountPercent = 5m,
                SkontoPercent = 2m
            },
            DateTimeOffset.Parse("2026-07-04T10:00:00+02:00"));

        Assert.Equal("Offerte Sanierung", model.ProjectTitle);
        Assert.Equal("Abwasser Uri", model.CustomerBlock);
        Assert.Equal("Objekt: Test", model.ObjectBlock);
        Assert.Equal("Ihre Referenz: PA", model.ReferenceBlock);
        Assert.Equal("3'000.00 CHF", model.Totals.GrossNetText);
        Assert.Equal("5 %: -150.00 CHF", model.Totals.DiscountText);
        Assert.Equal("2 %: -57.00 CHF", model.Totals.SkontoText);
        Assert.Equal("2'793.00 CHF", model.Totals.NetText);
        Assert.Equal("8.1 %: 226.23 CHF", model.Totals.VatText);
        Assert.Equal("3'019.23 CHF", model.Totals.TotalInclVatText);

        Assert.Collection(
            model.ChapterSummaryLines,
            first =>
            {
                Assert.Equal("100", first.Chapter);
                Assert.Contains("Einrichtung", first.Title);
                Assert.Equal("1'000.00 CHF", first.TotalText);
            },
            second =>
            {
                Assert.Equal("600", second.Chapter);
                Assert.Contains("Renovierung", second.Title);
                Assert.Equal("2'000.00 CHF", second.TotalText);
            });

        var liner = Assert.Single(model.PositionLines.Where(p => p.NpkCode == "612.110"));
        Assert.Equal("300", liner.DnText);
        Assert.Equal("10", liner.QtyText);
        Assert.Equal("200.00 CHF", liner.UnitPriceText);
        Assert.Equal("2'000.00 CHF", liner.TotalText);
    }

    [Fact]
    public void Create_markiert_variable_Preise_Pauschalen_und_Dubletten()
    {
        var positions = new List<AggregatedPosition>
        {
            new(
                NpkCode: "311.110",
                Chapter: "300",
                ItemKey: "A",
                Text: "Fraesen",
                Unit: "m",
                Dn: null,
                TotalQty: 10m,
                TotalNet: 1000m,
                HoldingCount: 1,
                IsVariablePrice: true,
                UnitPrice: null),
            new(
                NpkCode: "311.110",
                Chapter: "300",
                ItemKey: "B",
                Text: "Roboterarbeiten",
                Unit: "h",
                Dn: null,
                TotalQty: 2m,
                TotalNet: 500m,
                HoldingCount: 1,
                IsVariablePrice: false,
                UnitPrice: 250m)
        };

        var model = NpkOfferPdfModelFactory.Create(
            positions,
            new NpkOfferPdfContext { VatRate = 0.081m },
            DateTimeOffset.Parse("2026-07-04T10:00:00+02:00"),
            excludedPauschaleTotal: 123.45m,
            excludedPauschaleHoldingCount: 2);

        Assert.Contains(model.PositionLines, p => p.Text == "Fraesen" && p.UnitPriceText == "variabel");
        Assert.Contains(model.Footnotes, note => note.Contains("NPK 311.110", StringComparison.Ordinal));
        Assert.Contains(model.Footnotes, note => note.Contains("Nicht in NPK-Positionen enthaltene Pauschalkosten (2 Haltung(en)): 123.45 CHF", StringComparison.Ordinal));
    }
}
