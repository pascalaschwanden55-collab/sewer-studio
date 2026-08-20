using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageSummaryEntryBuilderTests
{
    [Fact]
    public void Build_uebernimmt_detailkosten_und_baut_fallbackkosten()
    {
        var detailedCost = new HoldingCost { Holding = "H-1", Total = 123m };
        var rows = new[]
        {
            Row("H-1", hasDetailedCost: true, storedCost: detailedCost, netCost: 0m),
            Row("H-2", hasDetailedCost: false, storedCost: null, netCost: 100m),
            Row("H-3", hasDetailedCost: false, storedCost: null, netCost: 0m)
        };

        var entries = BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.081m);

        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal("H-1", first.Holding);
                Assert.Equal("Gemeinde", first.Owner);
                Assert.Equal("Kanalsanierer", first.ExecutedBy);
                // Detailkosten werden inhaltlich uebernommen. Seit der MWST-Ergaenzung
                // ist es bewusst eine Kopie und nicht mehr dieselbe Instanz.
                Assert.Equal(detailedCost.Holding, first.Cost.Holding);
                Assert.Equal(123m, first.Cost.Total);
                Assert.Same(detailedCost.Measures, first.Cost.Measures);
            },
            second =>
            {
                Assert.Equal("H-2", second.Holding);
                Assert.Equal(100m, second.Cost.Total);
                Assert.Equal(8.10m, second.Cost.MwstAmount);
                Assert.Equal("PAUSCHALE", Assert.Single(second.Cost.Measures).MeasureId);
            });
    }

    [Fact]
    public void Build_nutzt_fallback_wenn_detailflag_ohne_kostenobjekt_kommt()
    {
        var rows = new[]
        {
            Row("H-1", hasDetailedCost: true, storedCost: null, netCost: 50m)
        };

        var entry = Assert.Single(BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.01m));

        Assert.Equal("H-1", entry.Holding);
        Assert.Equal(50m, entry.Cost.Total);
        Assert.Equal(0.50m, entry.Cost.MwstAmount);
    }

    /// <summary>
    /// Der Schacht-Massnahmen-Dialog speicherte die MWST-Felder nie (Fehler vom
    /// 2026-08-20). Solche Detailkosten erschienen im Druckcenter-Ausdruck ohne
    /// MWST. Beim Bauen der PDF-Eintraege wird sie jetzt aus dem Projektsatz
    /// ergaenzt — die gespeicherte Datei bleibt unveraendert.
    /// </summary>
    [Fact]
    public void Build_ergaenzt_fehlende_Mwst_an_gespeicherten_Detailkosten()
    {
        var schachtOhneMwst = new HoldingCost { Holding = "80551", Total = 1100m };
        var rows = new[] { Row("80551", hasDetailedCost: true, storedCost: schachtOhneMwst, netCost: 0m) };

        var entry = Assert.Single(BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.081m));

        Assert.Equal(0.081m, entry.Cost.MwstRate);
        Assert.Equal(89.10m, entry.Cost.MwstAmount);
        Assert.Equal(1189.10m, entry.Cost.TotalInclMwst);

        // Die gespeicherte Kostenquelle darf dabei nicht veraendert werden.
        Assert.Equal(0m, schachtOhneMwst.MwstAmount);
    }

    private static DruckcenterRowVm Row(
        string holding,
        bool hasDetailedCost,
        HoldingCost? storedCost,
        decimal netCost)
        => new()
        {
            Holding = holding,
            Owner = "Gemeinde",
            ExecutedBy = "Kanalsanierer",
            HasDetailedCost = hasDetailedCost,
            StoredCost = storedCost,
            NetCost = netCost
        };

}
