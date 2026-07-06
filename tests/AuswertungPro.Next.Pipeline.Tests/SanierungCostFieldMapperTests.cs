using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer SanierungCostFieldMapper, MeasureClassification und
/// MeasuresTextBuilder (reine Application-Logik, kein IO, kein WPF).
/// Auditregeln: W7 = Anschluss-Max statt Sum; Liner = 1 Stk; LEM != Manschette.
/// </summary>
public sealed class SanierungCostFieldMapperTests
{
    // =========================================================================
    // MeasureClassification — MatchesIdentifier
    // =========================================================================

    [Fact]
    public void MatchesIdentifier_ExakterGleicher_GibtTrue()
    {
        Assert.True(MeasureClassification.MatchesIdentifier("SCHLAUCHLINER_NADELFILZ", "SCHLAUCHLINER_NADELFILZ"));
    }

    [Fact]
    public void MatchesIdentifier_CaseInsensitive_GibtTrue()
    {
        Assert.True(MeasureClassification.MatchesIdentifier("schlauchliner_nadelfilz", "SCHLAUCHLINER_NADELFILZ"));
    }

    [Fact]
    public void MatchesIdentifier_EinfacherTokenSubstring_GibtTrue()
    {
        // "GFK" ohne Unterstrich matcht als Substring in "SCHLAUCHLINER_GFK"
        Assert.True(MeasureClassification.MatchesIdentifier("SCHLAUCHLINER_GFK", "GFK"));
    }

    [Fact]
    public void MatchesIdentifier_MusterMitUnterstrich_KeinSubstringMatch()
    {
        // Muster mit '_' = kein Substring-Fallback
        Assert.False(MeasureClassification.MatchesIdentifier("SCHLAUCHLINER_GFK", "SCHLAUCHLINER_NADELFILZ"));
    }

    [Fact]
    public void MatchesIdentifier_NullValue_GibtFalse()
    {
        Assert.False(MeasureClassification.MatchesIdentifier(null, "GFK"));
    }

    [Fact]
    public void MatchesIdentifier_LeererPattern_GibtFalse()
    {
        Assert.False(MeasureClassification.MatchesIdentifier("GFK", ""));
    }

    // =========================================================================
    // MeasureClassification — IsLinerIdentifier
    // =========================================================================

    [Theory]
    [InlineData("SCHLAUCHLINER_NADELFILZ")]
    [InlineData("SCHLAUCHLINER_GFK")]
    [InlineData("NADELFILZ")]
    [InlineData("GFK")]
    [InlineData("NADELFILZ_LINER_BIS_5M")]
    public void IsLinerIdentifier_LinerKeys_GibtTrue(string key)
    {
        Assert.True(MeasureClassification.IsLinerIdentifier(key));
    }

    [Theory]
    [InlineData("MANSCHETTE_PER_ST")]
    [InlineData("KURZLINER_PER_ST")]
    [InlineData("LINERENDMANSCHETTE_LEM")]
    [InlineData("")]
    [InlineData(null)]
    public void IsLinerIdentifier_NichtLinerKeys_GibtFalse(string? key)
    {
        Assert.False(MeasureClassification.IsLinerIdentifier(key));
    }

    // =========================================================================
    // MeasureClassification — IsHauptarbeitLine
    // =========================================================================

    [Fact]
    public void IsHauptarbeitLine_GroupHauptarbeit_GibtTrue()
    {
        var line = new CostLine { Group = "Hauptarbeit", ItemKey = "IRGENDWAS", Selected = true };
        Assert.True(MeasureClassification.IsHauptarbeitLine(line));
    }

    [Fact]
    public void IsHauptarbeitLine_ItemKeySchlauchliner_GibtTrue()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Selected = true };
        Assert.True(MeasureClassification.IsHauptarbeitLine(line));
    }

    [Fact]
    public void IsHauptarbeitLine_NebenkostenZeile_GibtFalse()
    {
        var line = new CostLine { Group = "Nebenarbeiten", ItemKey = "VERKEHRSDIENST", Text = "Verkehrsdienst" };
        Assert.False(MeasureClassification.IsHauptarbeitLine(line));
    }

    [Fact]
    public void IsHauptarbeitLine_Null_GibtFalse()
    {
        Assert.False(MeasureClassification.IsHauptarbeitLine(null));
    }

    // =========================================================================
    // MeasuresTextBuilder — NormalizeRecommendationEntry
    // =========================================================================

    [Fact]
    public void NormalizeRecommendationEntry_MitBullet_EntferntBullet()
    {
        Assert.Equal("Schlauchliner Nadelfilz", MeasuresTextBuilder.NormalizeRecommendationEntry("- Schlauchliner Nadelfilz"));
    }

    [Fact]
    public void NormalizeRecommendationEntry_MitSternBullet_EntferntStern()
    {
        Assert.Equal("Schlauchliner Nadelfilz", MeasuresTextBuilder.NormalizeRecommendationEntry("* Schlauchliner Nadelfilz"));
    }

    [Fact]
    public void NormalizeRecommendationEntry_OhneBullet_Unveraendert()
    {
        Assert.Equal("Schlauchliner", MeasuresTextBuilder.NormalizeRecommendationEntry("Schlauchliner"));
    }

    [Fact]
    public void NormalizeRecommendationEntry_Null_GibtLeerString()
    {
        Assert.Equal("", MeasuresTextBuilder.NormalizeRecommendationEntry(null));
    }

    // =========================================================================
    // MeasuresTextBuilder — FormatDecimal / FormatInt
    // =========================================================================

    [Fact]
    public void FormatDecimal_PositiverWert_GibtFormatiertString()
    {
        Assert.Equal("12.50", MeasuresTextBuilder.FormatDecimal(12.5m));
    }

    [Fact]
    public void FormatDecimal_NullOderNegativ_GibtLeerString()
    {
        Assert.Equal("", MeasuresTextBuilder.FormatDecimal(0m));
        Assert.Equal("", MeasuresTextBuilder.FormatDecimal(-5m));
    }

    [Fact]
    public void FormatInt_PositiverWert_GibtString()
    {
        Assert.Equal("3", MeasuresTextBuilder.FormatInt(3));
    }

    [Fact]
    public void FormatInt_NullOderNegativ_GibtLeerString()
    {
        Assert.Equal("", MeasuresTextBuilder.FormatInt(0));
        Assert.Equal("", MeasuresTextBuilder.FormatInt(-1));
    }

    // =========================================================================
    // SanierungCostFieldMapper — ResolveNetTotal
    // =========================================================================

    [Fact]
    public void ResolveNetTotal_TotalGesetzt_NimmtTotal()
    {
        var cost = new HoldingCost { Total = 1000m, TotalInclMwst = 1077m, MwstRate = 0.077m };
        Assert.Equal(1000m, SanierungCostFieldMapper.ResolveNetTotal(cost));
    }

    [Fact]
    public void ResolveNetTotal_TotalNullBruttoMwst_RechnetZurueck()
    {
        // 1077 / 1.077 = 1000.00
        var cost = new HoldingCost { Total = 0m, TotalInclMwst = 1077m, MwstRate = 0.077m };
        Assert.Equal(1000m, SanierungCostFieldMapper.ResolveNetTotal(cost));
    }

    [Fact]
    public void ResolveNetTotal_KeinTotal_KeinMwst_NimmtBrutto()
    {
        var cost = new HoldingCost { Total = 0m, TotalInclMwst = 500m, MwstRate = 0m };
        Assert.Equal(500m, SanierungCostFieldMapper.ResolveNetTotal(cost));
    }

    // =========================================================================
    // SanierungCostFieldMapper — SumMeasureLengths
    // =========================================================================

    [Fact]
    public void SumMeasureLengths_MitLengthMeters_Summiert()
    {
        var cost = MakeCostWithMeasures(
            MakeMeasure("SCHLAUCHLINER_NADELFILZ", lengthMeters: 15m),
            MakeMeasure("SCHLAUCHLINER_GFK", lengthMeters: 10m));

        var result = SanierungCostFieldMapper.SumMeasureLengths(cost,
            "SCHLAUCHLINER_NADELFILZ", "SCHLAUCHLINER_GFK");

        Assert.Equal(25m, result);
    }

    [Fact]
    public void SumMeasureLengths_OhneLengthMeters_NimmtMaxQtyInMeter()
    {
        var line1 = new CostLine { Unit = "m", Qty = 20m, Selected = true };
        var line2 = new CostLine { Unit = "m", Qty = 5m, Selected = true };
        var measure = new MeasureCost
        {
            MeasureId = "NADELFILZ",
            Lines = new List<CostLine> { line1, line2 }
        };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var result = SanierungCostFieldMapper.SumMeasureLengths(cost, "NADELFILZ");

        // Max der Meter-Zeilen: 20
        Assert.Equal(20m, result);
    }

    // =========================================================================
    // SanierungCostFieldMapper — HasSelectedLiner (Liner = 1 Stk)
    // =========================================================================

    [Fact]
    public void HasSelectedLiner_MitLinerZeile_GibtTrue()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Selected = true };
        var measure = new MeasureCost { MeasureId = "SCHLAUCHLINER_NADELFILZ", Lines = new List<CostLine> { line } };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        Assert.True(SanierungCostFieldMapper.HasSelectedLiner(cost));
    }

    [Fact]
    public void HasSelectedLiner_OhneLinerZeile_GibtFalse()
    {
        var line = new CostLine { ItemKey = "MANSCHETTE_PER_ST", Selected = true };
        var measure = new MeasureCost { MeasureId = "MANSCHETTE_PER_ST", Lines = new List<CostLine> { line } };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        Assert.False(SanierungCostFieldMapper.HasSelectedLiner(cost));
    }

    [Fact]
    public void HasSelectedLiner_LinerNichtSelected_GibtFalse()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Selected = false };
        var measure = new MeasureCost { MeasureId = "SCHLAUCHLINER_NADELFILZ", Lines = new List<CostLine> { line } };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        Assert.False(SanierungCostFieldMapper.HasSelectedLiner(cost));
    }

    // =========================================================================
    // Audit W7: MaxMeasureQty (Anschluss — kein Sum ueber Massnahmen)
    // =========================================================================

    [Fact]
    public void MaxMeasureQty_ZweiMassnahmenMitGleicherAnschlusszahl_NimmtMax()
    {
        // 2 Massnahmen-Buendel, jedes hat 3 Anschluesse injiziert — Ergebnis = 3 (nicht 6)
        var cost = MakeCostWithMeasures(
            MakeMeasureWithLine("M1", "ANSCHLUSS_EINBINDEN", 3m),
            MakeMeasureWithLine("M2", "ANSCHLUSS_EINBINDEN", 3m));

        var result = SanierungCostFieldMapper.MaxMeasureQty(cost, "ANSCHLUSS_EINBINDEN");

        Assert.Equal(3, result);
    }

    [Fact]
    public void MaxMeasureQty_VerschiedeneAnschlusszahlen_NimmtGroesste()
    {
        var cost = MakeCostWithMeasures(
            MakeMeasureWithLine("M1", "ANSCHLUSS_EINBINDEN", 2m),
            MakeMeasureWithLine("M2", "ANSCHLUSS_EINBINDEN", 5m));

        var result = SanierungCostFieldMapper.MaxMeasureQty(cost, "ANSCHLUSS_EINBINDEN");

        Assert.Equal(5, result);
    }

    // =========================================================================
    // Audit: LEM != Manschette
    // =========================================================================

    [Fact]
    public void SumSelectedQty_LEM_FuelltNurLEMFeld()
    {
        // LEM darf NICHT in Reparatur_Manschette einfliessen
        var lineLEM = new CostLine { ItemKey = "LINERENDMANSCHETTE_LEM", Qty = 2m, Selected = true };
        var lineManschette = new CostLine { ItemKey = "MANSCHETTE_PER_ST", Qty = 1m, Selected = true };
        var measure = new MeasureCost
        {
            MeasureId = "M",
            Lines = new List<CostLine> { lineLEM, lineManschette }
        };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var lem = SanierungCostFieldMapper.SumSelectedQty(cost, "LINERENDMANSCHETTE_LEM");
        var manschette = SanierungCostFieldMapper.SumSelectedQty(cost, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");

        Assert.Equal(2, lem);
        Assert.Equal(1, manschette);
    }

    // =========================================================================
    // SanierungCostFieldMapper — ClearCosts
    // =========================================================================

    [Fact]
    public void ClearCosts_AlleKostenfelder_WerdenGeleert()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Kosten", "1234.00", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", "Schlauchliner", FieldSource.Manual, userEdited: true);

        SanierungCostFieldMapper.ClearCosts(record);

        Assert.Equal("", record.GetFieldValue("Kosten"));
        Assert.Equal("", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
    }

    [Fact]
    public void ClearCosts_NullRecord_WirftKeineException()
    {
        // Soll stumm bleiben (null-Guard)
        SanierungCostFieldMapper.ClearCosts(null!);
    }

    // =========================================================================
    // SanierungCostFieldMapper — ApplyCosts End-to-End
    // =========================================================================

    [Fact]
    public void ApplyCosts_LinerMassnahme_SetztzInlinerStk1()
    {
        var linerLine = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Selected = true, Unit = "m", Qty = 25m };
        var measure = new MeasureCost
        {
            MeasureId = "SCHLAUCHLINER_NADELFILZ",
            MeasureName = "Schlauchliner Nadelfilz",
            LengthMeters = 25m,
            Lines = new List<CostLine> { linerLine }
        };
        var cost = new HoldingCost
        {
            Total = 5000m,
            Measures = new List<MeasureCost> { measure }
        };
        var record = new HaltungRecord();

        SanierungCostFieldMapper.ApplyCosts(record, cost);

        Assert.Equal("5000.00", record.GetFieldValue("Kosten"));
        Assert.Equal("1", record.GetFieldValue("Renovierung_Inliner_Stk"));
        Assert.Equal("25.00", record.GetFieldValue("Renovierung_Inliner_m"));
    }

    [Fact]
    public void ApplyCosts_IncludeCostsFalse_KeineKostenUebertragen()
    {
        var cost = new HoldingCost { Total = 9999m, Measures = new List<MeasureCost>() };
        var record = new HaltungRecord();

        SanierungCostFieldMapper.ApplyCosts(record, cost, includeCosts: false);

        Assert.Equal("", record.GetFieldValue("Kosten"));
    }

    [Fact]
    public void ApplyCosts_AnschlussW7_MaxStattSum()
    {
        // 2 Massnahmen-Buendel, jedes hat 3 Anschluesse — Ergebnis soll 3 sein (Audit W7)
        var cost = MakeCostWithMeasures(
            MakeMeasureWithLine("M1", "ANSCHLUSS_EINBINDEN", 3m),
            MakeMeasureWithLine("M2", "ANSCHLUSS_EINBINDEN", 3m));
        var record = new HaltungRecord();

        SanierungCostFieldMapper.ApplyCosts(record, cost);

        Assert.Equal("3", record.GetFieldValue("Anschluesse_verpressen"));
    }

    // =========================================================================
    // MeasuresTextBuilder — BuildMeasuresText
    // =========================================================================

    [Fact]
    public void BuildMeasuresText_HauptarbeitMassnahme_SchreibtMassnahmenName()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Group = "Hauptarbeit", Selected = true };
        var measure = new MeasureCost
        {
            MeasureId = "SCHLAUCHLINER_NADELFILZ",
            MeasureName = "Schlauchliner Nadelfilz",
            Lines = new List<CostLine> { line }
        };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var text = MeasuresTextBuilder.BuildMeasuresText(cost);

        Assert.Equal("Schlauchliner Nadelfilz", text);
    }

    [Fact]
    public void BuildMeasuresText_KeineHauptarbeit_GibtLeerString()
    {
        var line = new CostLine { Group = "Nebenarbeiten", ItemKey = "VERKEHRSDIENST", Selected = true };
        var measure = new MeasureCost
        {
            MeasureId = "VERKEHRSDIENST",
            MeasureName = "Verkehrsdienst",
            Lines = new List<CostLine> { line }
        };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var text = MeasuresTextBuilder.BuildMeasuresText(cost);

        Assert.Equal("", text);
    }

    [Fact]
    public void BuildMeasuresText_MarkiertePosition_HatVorrangVorMassnahmenName()
    {
        // Die Hauptarbeit-Massnahme wuerde "Schlauchliner Nadelfilz" liefern — aber der
        // Nutzer hat eine Position in der Uebertrag-Spalte markiert -> Markierung gewinnt.
        var haupt = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Group = "Hauptarbeit", Selected = true };
        var markiert = new CostLine { Text = "Fräsen / Hindernisse entfernen", TransferMarked = true };
        var measure = new MeasureCost
        {
            MeasureId = "SCHLAUCHLINER_NADELFILZ",
            MeasureName = "Schlauchliner Nadelfilz",
            Lines = new List<CostLine> { haupt, markiert }
        };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var text = MeasuresTextBuilder.BuildMeasuresText(cost);

        Assert.Equal("Fräsen / Hindernisse entfernen", text);
    }

    [Fact]
    public void BuildMeasuresText_MehrereMarkierte_JedeAufEigenerZeile()
    {
        var l1 = new CostLine { Text = "Schlauchliner (GFK)", TransferMarked = true };
        var l2 = new CostLine { Text = "Anschluss auffräsen", TransferMarked = true };
        var measure = new MeasureCost { MeasureId = "M", MeasureName = "M", Lines = new List<CostLine> { l1, l2 } };
        var cost = new HoldingCost { Measures = new List<MeasureCost> { measure } };

        var text = MeasuresTextBuilder.BuildMeasuresText(cost);

        Assert.Equal("Schlauchliner (GFK)" + Environment.NewLine + "Anschluss auffräsen", text);
    }

    [Fact]
    public void ApplyCosts_MitMarkierterPosition_SchreibtMarkiertenTextInsFeld()
    {
        var markiert = new CostLine
        {
            Text = "Schlauchliner (GFK)", TransferMarked = true, Selected = true,
            Unit = "m", Qty = 30m, UnitPrice = 165m
        };
        var measure = new MeasureCost
        {
            MeasureId = "SCHLAUCHLINER_GFK",
            MeasureName = "Schlauchliner GFK",
            Lines = new List<CostLine> { markiert }
        };
        var cost = new HoldingCost { Total = 5000m, Measures = new List<MeasureCost> { measure } };
        var record = new HaltungRecord();

        SanierungCostFieldMapper.ApplyCosts(record, cost);

        Assert.Equal("Schlauchliner (GFK)", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
    }

    // =========================================================================
    // Hilfsmethoden
    // =========================================================================

    private static HoldingCost MakeCostWithMeasures(params MeasureCost[] measures)
        => new() { Measures = new List<MeasureCost>(measures) };

    private static MeasureCost MakeMeasure(string id, decimal? lengthMeters = null)
        => new() { MeasureId = id, MeasureName = id, LengthMeters = lengthMeters, Lines = new List<CostLine>() };

    private static MeasureCost MakeMeasureWithLine(string measureId, string itemKey, decimal qty)
    {
        var line = new CostLine { ItemKey = itemKey, Qty = qty, Selected = true };
        return new MeasureCost
        {
            MeasureId = measureId,
            MeasureName = measureId,
            Lines = new List<CostLine> { line }
        };
    }
}
