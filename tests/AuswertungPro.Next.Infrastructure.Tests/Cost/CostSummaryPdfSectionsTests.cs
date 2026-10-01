using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using Xunit;
using AuswertungPro.Next.Application.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Die Kostenzusammenstellung war faktisch eine Volldokumentation: der groesste
/// Abschnitt ("Komplette Aufstellung Einzelpositionen", bei 58 Haltungen rund 19 Seiten)
/// liess sich als einziger NICHT abschalten. Jeder Abschnitt braucht seinen Schalter,
/// und der Standard muss schlank sein.
/// </summary>
public sealed class CostSummaryPdfSectionsTests
{
    [Fact]
    public void Schlank_laesst_die_grossen_Abschnitte_weg()
    {
        var s = CostSummaryPdfSections.Schlank;

        Assert.True(s.OwnerSummary);
        Assert.True(s.MeasureSummary);
        Assert.False(s.FullPositionList);
        Assert.False(s.DataOverview);
        Assert.False(s.PositionSummary);
        Assert.False(s.SpecialStats);
        Assert.False(s.ExecutorStats);
    }

    [Fact]
    public void Alles_schaltet_jeden_Abschnitt_ein()
    {
        var s = CostSummaryPdfSections.Alles;

        Assert.True(s.OwnerSummary);
        Assert.True(s.MeasureSummary);
        Assert.True(s.FullPositionList);
        Assert.True(s.DataOverview);
        Assert.True(s.PositionSummary);
        Assert.True(s.SpecialStats);
        Assert.True(s.ExecutorStats);
    }

    [Fact]
    public void Abgeschaltete_Vollaufstellung_erzeugt_keine_Einzelzeilen()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(),
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank);

        Assert.Empty(model.Lines);
    }

    [Fact]
    public void Eingeschaltete_Vollaufstellung_erzeugt_die_Einzelzeilen()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(),
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Alles);

        Assert.Equal(2, model.Lines.Count);
    }

    /// <summary>
    /// Die Totale sind der Kern des Ausdrucks und duerfen sich durch das Abschalten
    /// eines Abschnitts NIE veraendern — sonst stuende je nach Haekchen ein anderer Betrag da.
    /// </summary>
    [Fact]
    public void Die_Totale_bleiben_unabhaengig_von_den_Abschnitten_gleich()
    {
        var schlank = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(), new OfferPdfContext(), DateTimeOffset.Now, CostSummaryPdfSections.Schlank);
        var alles = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(), new OfferPdfContext(), DateTimeOffset.Now, CostSummaryPdfSections.Alles);

        Assert.Equal(alles.Totals.NetText, schlank.Totals.NetText);
        Assert.Equal(alles.Totals.GrossText, schlank.Totals.GrossText);
        Assert.Equal(alles.Totals.VatText, schlank.Totals.VatText);
    }

    [Fact]
    public void Abgeschaltete_Massnahmenuebersicht_bleibt_leer()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(),
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank with { MeasureSummary = false });

        Assert.Empty(model.MeasureSummaryLines);
    }

    [Fact]
    public void Abgeschaltete_Eigentuemeruebersicht_bleibt_leer()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(),
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank with { OwnerSummary = false });

        Assert.Empty(model.OwnerSummaryLines);
    }

    /// <summary>
    /// Die Datenuebersicht kommt von aussen. Ist der Abschnitt aus, darf sie auch dann
    /// nicht im Modell landen, wenn der Aufrufer sie mitgibt.
    /// </summary>
    [Fact]
    public void Abgeschaltete_Datenuebersicht_verwirft_uebergebene_Zeilen()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(),
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank with { DataOverview = false },
            [new OfferPdfHoldingDataLineModel { Holding = "H-1" }]);

        Assert.Empty(model.HoldingDataLines);
    }

    /// <summary>Kennzahlen fuer den Kopfstreifen auf Seite 1.</summary>
    [Fact]
    public void Die_Anzahl_der_Bauteile_steht_fuer_den_Kennzahlenstreifen_bereit()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            Eintraege(), new OfferPdfContext(), DateTimeOffset.Now, CostSummaryPdfSections.Schlank);

        Assert.Equal(2, model.Totals.EntryCount);
    }

    private static List<CostSummaryEntry> Eintraege()
        =>
        [
            Eintrag("H-1", "AWU", "Liner", 100m),
            Eintrag("H-2", "Privat", "Manschette", 50m)
        ];

    private static CostSummaryEntry Eintrag(string holding, string owner, string text, decimal preis)
        => new()
        {
            Holding = holding,
            Owner = owner,
            ExecutedBy = "Firma A",
            Cost = new HoldingCost
            {
                Holding = holding,
                Total = preis,
                Measures =
                [
                    new MeasureCost
                    {
                        MeasureId = "M1",
                        MeasureName = "Sanierung",
                        Total = preis,
                        Lines =
                        [
                            new CostLine
                            {
                                Group = "Hauptarbeit",
                                ItemKey = "K1",
                                Text = text,
                                Unit = "m",
                                Qty = 1m,
                                UnitPrice = preis,
                                Selected = true
                            }
                        ]
                    }
                ]
            }
        };
}
