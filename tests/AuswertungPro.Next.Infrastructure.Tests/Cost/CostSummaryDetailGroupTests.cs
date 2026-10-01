using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Die Detailliste steht zwischen der einzeiligen Datenuebersicht und der 19-seitigen
/// Vollaufstellung: je Bauteil eine Kopfzeile mit Total, darunter die Massnahmen mit
/// Menge und Betrag. Haltungen und Schaechte bilden getrennte Gruppen mit eigenem
/// Zwischentotal.
/// </summary>
public sealed class CostSummaryDetailGroupTests
{
    [Fact]
    public void Detailliste_gruppiert_nach_Bauteilart_mit_Zwischentotal()
    {
        var model = Baue(
            [
                Eintrag("H-1", "Haltungen", "AWU", ("Schlauchliner GFK", "m", 32m, 225m)),
                Eintrag("H-2", "Haltungen", "AWU", ("Kurzliner", "Stk", 1m, 1200m)),
                Eintrag("S-1", "Schächte", "AWU", ("Schachthals sanieren", "Stk", 1m, 400m))
            ]);

        Assert.Equal(2, model.DetailGroups.Count);

        var haltungen = model.DetailGroups[0];
        Assert.Equal("Haltungen", haltungen.Title);
        Assert.Equal(2, haltungen.Entries.Count);
        Assert.Equal("2", haltungen.EntryCountText);

        var schaechte = model.DetailGroups[1];
        Assert.Equal("Schächte", schaechte.Title);
        Assert.Single(schaechte.Entries);
    }

    [Fact]
    public void Ein_Eintrag_traegt_Kopfzeile_und_seine_Massnahmen()
    {
        var model = Baue(
            [Eintrag("80551-80534", "Haltungen", "AWU", ("Schlauchliner GFK", "m", 32m, 225m))]);

        var entry = Assert.Single(model.DetailGroups[0].Entries);
        Assert.Equal("80551-80534", entry.Name);
        Assert.Equal("AWU", entry.Owner);

        var measure = Assert.Single(entry.Measures);
        Assert.Equal("Sanierung", measure.Name);
        Assert.Equal("32 m", measure.QtyText);
    }

    /// <summary>
    /// Mengen mit verschiedenen Einheiten lassen sich nicht addieren. Dann bleibt die
    /// Mengenspalte leer, statt eine erfundene Zahl auszuweisen.
    /// </summary>
    [Fact]
    public void Gemischte_Einheiten_ergeben_keine_erfundene_Menge()
    {
        var model = Baue(
            [Eintrag("H-1", "Haltungen", "AWU",
                ("Liner", "m", 10m, 100m),
                ("Manschette", "Stk", 2m, 300m))]);

        var measure = Assert.Single(model.DetailGroups[0].Entries[0].Measures);
        Assert.Equal("", measure.QtyText);
    }

    [Fact]
    public void Gleiche_Einheiten_werden_zusammengezaehlt()
    {
        var model = Baue(
            [Eintrag("H-1", "Haltungen", "AWU",
                ("Liner", "m", 10m, 100m),
                ("Reinigung", "m", 8m, 50m))]);

        var measure = Assert.Single(model.DetailGroups[0].Entries[0].Measures);
        Assert.Equal("18 m", measure.QtyText);
    }

    /// <summary>
    /// Die Summe der Gruppen muss dem Gesamttotal entsprechen — sonst widerspricht
    /// sich der Ausdruck auf zwei Seiten.
    /// </summary>
    [Fact]
    public void Die_Zwischentotale_ergeben_zusammen_das_Gesamttotal()
    {
        var model = Baue(
            [
                Eintrag("H-1", "Haltungen", "AWU", ("Liner", "m", 1m, 700m)),
                Eintrag("S-1", "Schächte", "AWU", ("Schachthals", "Stk", 1m, 300m))
            ]);

        Assert.Equal(2, model.DetailGroups.Count);
        Assert.Contains("700", model.DetailGroups[0].SubtotalText);
        Assert.Contains("300", model.DetailGroups[1].SubtotalText);
        Assert.Contains("1", model.Totals.NetText);
    }

    [Fact]
    public void Abgeschaltete_Detailliste_bleibt_leer()
    {
        var model = OfferPdfModelFactory.CreateCostSummary(
            [Eintrag("H-1", "Haltungen", "AWU", ("Liner", "m", 1m, 700m))],
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank with { DetailList = false });

        Assert.Empty(model.DetailGroups);
    }

    /// <summary>Ohne Gruppenangabe bleibt der Ausdruck einbereichig — eine Gruppe.</summary>
    [Fact]
    public void Ohne_Gruppenangabe_entsteht_eine_einzige_Gruppe()
    {
        var model = Baue([Eintrag("H-1", "", "AWU", ("Liner", "m", 1m, 700m))]);

        var group = Assert.Single(model.DetailGroups);
        Assert.Single(group.Entries);
    }

    /// <summary>
    /// Frei eingegebene Massnahmen (Schacht-Dialog) tragen keinen Katalog-ItemKey. Ihr
    /// Sammelname ist immer "Empfohlene Massnahmen" und sagt nichts — die Information
    /// steckt im Text jeder einzelnen Zeile. Darum wird hier je Zeile aufgeschluesselt.
    /// </summary>
    [Fact]
    public void Frei_eingegebene_Massnahmen_erscheinen_einzeln_statt_als_Sammelname()
    {
        var model = Baue([FreieMassnahmen("80844", "Schächte", "AWU",
            ("Gerinne sanieren", 1m, 500m),
            ("Anschluss sanieren", 1m, 500m))]);

        var measures = model.DetailGroups[0].Entries[0].Measures;

        Assert.Equal(2, measures.Count);
        Assert.Equal("Gerinne sanieren", measures[0].Name);
        Assert.Equal("Anschluss sanieren", measures[1].Name);
        Assert.DoesNotContain(measures, m => m.Name == "Empfohlene Massnahmen");
    }

    [Fact]
    public void Frei_eingegebene_Massnahmen_behalten_ihren_Einzelbetrag()
    {
        var model = Baue([FreieMassnahmen("80844", "Schächte", "AWU",
            ("Gerinne sanieren", 1m, 500m),
            ("Anschluss sanieren", 2m, 250m))]);

        var measures = model.DetailGroups[0].Entries[0].Measures;

        Assert.Contains("500", measures[0].TotalText);
        Assert.Contains("500", measures[1].TotalText);
        Assert.Equal("2 Stk", measures[1].QtyText);
    }

    /// <summary>
    /// Katalogpositionen bleiben zur Massnahme zusammengefasst — sonst waere die
    /// Detailliste wieder die 19-seitige Vollaufstellung.
    /// </summary>
    [Fact]
    public void Katalogpositionen_bleiben_zur_Massnahme_zusammengefasst()
    {
        var model = Baue(
            [Eintrag("H-1", "Haltungen", "AWU",
                ("GFK-Liner", "m", 30m, 200m),
                ("Reinigung", "m", 30m, 20m))]);

        var measure = Assert.Single(model.DetailGroups[0].Entries[0].Measures);
        Assert.Equal("Sanierung", measure.Name);
    }

    private static CostSummaryEntry FreieMassnahmen(
        string name,
        string gruppe,
        string owner,
        params (string Text, decimal Qty, decimal Price)[] zeilen)
        => new()
        {
            Holding = name,
            GroupLabel = gruppe,
            Owner = owner,
            Cost = new HoldingCost
            {
                Holding = name,
                Total = zeilen.Sum(z => z.Qty * z.Price),
                Measures =
                [
                    new MeasureCost
                    {
                        MeasureId = "SCHACHT_EMPFEHLUNG",
                        MeasureName = "Empfohlene Massnahmen",
                        Total = zeilen.Sum(z => z.Qty * z.Price),
                        Lines = zeilen
                            .Select(z => new CostLine
                            {
                                ItemKey = "",
                                Text = z.Text,
                                Unit = "Stk",
                                Qty = z.Qty,
                                UnitPrice = z.Price,
                                Selected = true
                            })
                            .ToList()
                    }
                ]
            }
        };

    private static OfferPdfModel Baue(List<CostSummaryEntry> entries)
        => OfferPdfModelFactory.CreateCostSummary(
            entries,
            new OfferPdfContext(),
            DateTimeOffset.Now,
            CostSummaryPdfSections.Schlank with { DetailList = true });

    private static CostSummaryEntry Eintrag(
        string name,
        string gruppe,
        string owner,
        params (string Text, string Unit, decimal Qty, decimal Price)[] zeilen)
        => new()
        {
            Holding = name,
            GroupLabel = gruppe,
            Owner = owner,
            ExecutedBy = "Firma A",
            Cost = new HoldingCost
            {
                Holding = name,
                Total = zeilen.Sum(z => z.Qty * z.Price),
                Measures =
                [
                    new MeasureCost
                    {
                        MeasureId = "M1",
                        MeasureName = "Sanierung",
                        Total = zeilen.Sum(z => z.Qty * z.Price),
                        Lines = zeilen
                            .Select(z => new CostLine
                            {
                                Group = "Hauptarbeit",
                                ItemKey = z.Text,
                                Text = z.Text,
                                Unit = z.Unit,
                                Qty = z.Qty,
                                UnitPrice = z.Price,
                                Selected = true
                            })
                            .ToList()
                    }
                ]
            }
        };
}
