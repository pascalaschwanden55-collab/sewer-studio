using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus DataPageViewModel extrahierte Kosten-/Empfehlungs-Abbildung
/// auf den HaltungRecord ab. Reine Feld-Mapping-Logik, kein UI/Service.
/// </summary>
public sealed class DataPageSanierungCostMapperTests
{
    // --- ApplyRecommendation ---

    [Fact]
    public void ApplyRecommendation_setzt_alle_felder_wenn_werte_vorhanden()
    {
        var record = new HaltungRecord();
        var rec = new MeasureRecommendationResult(
            Measures: new[] { "Inliner", "Manschette" },
            EstimatedTotalCost: 1234.5m,
            RenovierungInlinerM: 12.3m,
            RenovierungInlinerStk: 1,
            AnschluesseVerpressen: 2,
            ReparaturManschette: 3,
            ReparaturKurzliner: 4,
            SimilarCasesCount: 7,
            UsedTrainedModel: true);

        DataPageSanierungCostMapper.ApplyRecommendation(record, rec);

        Assert.Equal(
            string.Join(Environment.NewLine, new[] { "Inliner", "Manschette" }),
            record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        Assert.Equal("1234.50", record.GetFieldValue("Kosten"));
        Assert.Equal("12.30", record.GetFieldValue("Renovierung_Inliner_m"));
        Assert.Equal("1", record.GetFieldValue("Renovierung_Inliner_Stk"));
        Assert.Equal("2", record.GetFieldValue("Anschluesse_verpressen"));
        Assert.Equal("3", record.GetFieldValue("Reparatur_Manschette"));
        Assert.Equal("4", record.GetFieldValue("Reparatur_Kurzliner"));
    }

    [Fact]
    public void ApplyRecommendation_ueberspringt_null_felder()
    {
        var record = new HaltungRecord();
        var rec = new MeasureRecommendationResult(
            Measures: new[] { "Inliner" },
            EstimatedTotalCost: null,
            RenovierungInlinerM: null,
            RenovierungInlinerStk: null,
            AnschluesseVerpressen: null,
            ReparaturManschette: null,
            ReparaturKurzliner: null,
            SimilarCasesCount: null,
            UsedTrainedModel: false);

        DataPageSanierungCostMapper.ApplyRecommendation(record, rec);

        Assert.Equal("Inliner", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Kosten")));
        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Renovierung_Inliner_m")));
        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Renovierung_Inliner_Stk")));
    }

    // --- ApplyCosts ---

    [Fact]
    public void ApplyCosts_schreibt_nettobetrag_massnahmen_und_liner()
    {
        var record = new HaltungRecord();
        var cost = new HoldingCost
        {
            Holding = "12.034-12.035",
            Total = 1000m,
            Measures =
            {
                new MeasureCost
                {
                    MeasureId = "SCHLAUCHLINER_NADELFILZ",
                    MeasureName = "Schlauchliner Nadelfilz",
                    LengthMeters = 10m,
                    Lines =
                    {
                        new CostLine
                        {
                            Group = "Hauptarbeit",
                            ItemKey = "SCHLAUCHLINER_NADELFILZ",
                            Text = "Schlauchliner",
                            Unit = "m",
                            Qty = 10m,
                            Selected = true,
                        },
                    },
                },
            },
        };

        DataPageSanierungCostMapper.ApplyCosts(record, cost, includeCosts: true);

        Assert.Equal("1000.00", record.GetFieldValue("Kosten"));
        Assert.Equal("Schlauchliner Nadelfilz", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        Assert.Equal("10.00", record.GetFieldValue("Renovierung_Inliner_m"));
        Assert.Equal("1", record.GetFieldValue("Renovierung_Inliner_Stk"));
    }

    [Fact]
    public void ApplyCosts_ohne_includeCosts_laesst_kosten_feld_leer()
    {
        var record = new HaltungRecord();
        var cost = new HoldingCost { Holding = "x", Total = 500m };

        DataPageSanierungCostMapper.ApplyCosts(record, cost, includeCosts: false);

        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Kosten")));
    }

    [Fact]
    public void ApplyCosts_leitet_netto_aus_bruttobetrag_ab_wenn_total_null()
    {
        var record = new HaltungRecord();
        var cost = new HoldingCost
        {
            Holding = "x",
            Total = 0m,
            TotalInclMwst = 108m,
            MwstRate = 0.08m,
        };

        DataPageSanierungCostMapper.ApplyCosts(record, cost, includeCosts: true);

        Assert.Equal("100.00", record.GetFieldValue("Kosten"));
    }

    [Fact]
    public void ApplyCosts_summiert_ausgewaehlte_manschetten_menge()
    {
        var record = new HaltungRecord();
        var cost = new HoldingCost
        {
            Holding = "x",
            Total = 1m,
            Measures =
            {
                new MeasureCost
                {
                    MeasureId = "MANSCHETTE",
                    MeasureName = "Manschette",
                    Lines =
                    {
                        new CostLine { ItemKey = "MANSCHETTE_PER_ST", Text = "Manschette", Qty = 2m, Selected = true },
                        new CostLine { ItemKey = "MANSCHETTE_EDELSTAHL", Text = "Edelstahl", Qty = 1m, Selected = true },
                        new CostLine { ItemKey = "MANSCHETTE_PER_ST", Text = "nicht gewaehlt", Qty = 5m, Selected = false },
                    },
                },
            },
        };

        DataPageSanierungCostMapper.ApplyCosts(record, cost, includeCosts: true);

        Assert.Equal("3", record.GetFieldValue("Reparatur_Manschette"));
    }

    // --- NormalizeRecommendationEntry (oeffentlich; vom ViewModel-ParseRecommendedTemplates genutzt) ---

    [Theory]
    [InlineData("- Inliner", "Inliner")]
    [InlineData("* Manschette", "Manschette")]
    [InlineData("  --* Kurzliner ", "Kurzliner")]
    [InlineData("Inliner", "Inliner")]
    public void NormalizeRecommendationEntry_entfernt_fuehrende_bullets(string input, string expected)
    {
        Assert.Equal(expected, DataPageSanierungCostMapper.NormalizeRecommendationEntry(input));
    }
}
