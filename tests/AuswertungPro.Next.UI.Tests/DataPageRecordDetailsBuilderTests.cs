using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b: die Haltungs-Eingabefelder folgen den vier festen Themen aus dem
/// Prototyp (Inventar 9.1). Fix-Runde 1: die Prototyp-Liste ist eine Mindestliste, keine
/// Ausschlussliste — drei fachlich zugehoerige Felder (Schacht_oben/Schacht_unten, Gefaelle,
/// Renovierung_Inliner_Stk), die der Prototyp nicht kennt, sind ergaenzt. Alles Uebrige
/// landet in "Weitere Angaben".
/// </summary>
public sealed class DataPageRecordDetailsBuilderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projektgefaelle_ist_einmal_editierbar_und_wird_vom_bericht_verwendet(bool vorhanden)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, true);
        if (vorhanden)
            record.SetFieldValue(FieldKeys.SlopePromille, "1", FieldSource.Manual, true);
        var factory = new DataPageDetailItemFactory(_ => null,
            (r, key, value) => r.SetFieldValue(key, value, FieldSource.Manual, true));

        var groups = DataPageRecordDetailsBuilder.Build(record, key => factory.Create(key, record));
        var slope = Assert.Single(groups.SelectMany(g => g.Items), i => i.FieldName == FieldKeys.SlopePromille);
        Assert.Equal("Gefälle ‰", slope.Label);
        // Fix-Runde 1: Das Gefaelle ist eine bewusste Ergaenzung der Stammdaten (CLAUDE.md:
        // immer als Stammdaten-Eingabe), auch wenn Inventar 9.1 es nicht auffuehrt.
        Assert.Contains(slope, groups.Single(g => g.Kind == RecordDetailGroupKind.MasterData).Items);
        slope.Value = "2,5";

        var calculation = AuswertungPro.Next.Application.DataPage.DataPageHydraulikReportCalculator
            .BuildReportCalculation(record, new AuswertungPro.Next.Application.Hydraulik.HydraulikPanelSettings());
        Assert.NotNull(calculation);
        Assert.Equal(2.5, calculation.Gefaelle_Promille);
    }

    // Anfangs- und Endschacht stehen nicht im Feldkatalog. Fix-Runde 1: sie sind trotzdem eine
    // bewusste Ergaenzung der Stammdaten (der Haltungsname haengt an den Schaechten), nicht
    // Teil von "Weitere Angaben".
    [Fact]
    public void Build_stellt_Anfangs_und_Endschacht_zu_den_Stammdaten()
    {
        var record = new HaltungRecord();
        record.Fields["Schacht_oben"] = "36262";
        record.Fields["Schacht_unten"] = "36275";

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var stammdaten = groups.Single(g => g.Title == "Stammdaten");
        Assert.Contains(stammdaten.Items, item => item.Label == "Schacht_oben");
        Assert.Contains(stammdaten.Items, item => item.Label == "Schacht_unten");

        var weitere = groups.SingleOrDefault(g => g.Title == "Weitere Angaben");
        if (weitere is not null)
        {
            Assert.DoesNotContain(weitere.Items, item => item.Label == "Schacht_oben");
            Assert.DoesNotContain(weitere.Items, item => item.Label == "Schacht_unten");
        }
    }

    /// <summary>
    /// Jedes Feld der vier Prototyp-Themen (Inventar 9.1, ergaenzt um die drei Fix-Runde-1-
    /// Felder) und eine Auswahl bekannter, aber bewusst nicht zugeordneter Felder — alles
    /// Uebrige (inklusive eines unbekannten Feldes) muss "Weitere Angaben" ergeben.
    /// </summary>
    [Theory]
    // Stammdaten (17 = 14 aus Inventar 9.1 + Schacht_oben/Schacht_unten + Gefaelle)
    [InlineData(FieldKeys.HoldingName, "Stammdaten")]
    [InlineData(FieldKeys.Street, "Stammdaten")]
    [InlineData("Schacht_oben", "Stammdaten")]
    [InlineData("Schacht_unten", "Stammdaten")]
    [InlineData(FieldKeys.PipeMaterial, "Stammdaten")]
    [InlineData(FieldKeys.NominalDiameterMm, "Stammdaten")]
    [InlineData(FieldKeys.ProfileType, "Stammdaten")]
    [InlineData(FieldKeys.ClearWidthMm, "Stammdaten")]
    [InlineData(FieldKeys.UsageType, "Stammdaten")]
    [InlineData(FieldKeys.HoldingLengthMeters, "Stammdaten")]
    [InlineData(FieldKeys.SlopePromille, "Stammdaten")]
    [InlineData("Inspektionsrichtung", "Stammdaten")]
    [InlineData(FieldKeys.InspectionYear, "Stammdaten")]
    [InlineData(FieldKeys.ConstructionYear, "Stammdaten")]
    [InlineData(FieldKeys.Owner, "Stammdaten")]
    [InlineData(FieldKeys.GeonisId, "Stammdaten")]
    [InlineData(FieldKeys.CadastreObjectId, "Stammdaten")]
    // Bewertung (9)
    [InlineData(FieldKeys.ConditionClass, "Bewertung")]
    [InlineData("VSA_Zustandsnote_D", "Bewertung")]
    [InlineData("VSA_Zustandsnote_S", "Bewertung")]
    [InlineData("VSA_Zustandsnote_B", "Bewertung")]
    [InlineData("VSA_Geschaetzt", "Bewertung")]
    [InlineData("Pruefungsresultat", "Bewertung")]
    [InlineData("Referenzpruefung", "Bewertung")]
    [InlineData("Gewaesserschutz", "Bewertung")]
    [InlineData("Grundwasserspiegel", "Bewertung")]
    // Sanierung (11 = 10 aus Inventar 9.1 + Renovierung_Inliner_Stk)
    [InlineData(FieldKeys.RenovationDecision, "Sanierung")]
    [InlineData(FieldKeys.RecommendedRehabilitationMeasures, "Sanierung")]
    [InlineData(FieldKeys.LinerRenovationMeters, "Sanierung")]
    [InlineData(FieldKeys.LinerRenovationCount, "Sanierung")]
    [InlineData(FieldKeys.ConnectionsToGrout, "Sanierung")]
    [InlineData(FieldKeys.RepairSleeve, "Sanierung")]
    [InlineData(FieldKeys.LinerEndSleeve, "Sanierung")]
    [InlineData(FieldKeys.ShortLinerRepair, "Sanierung")]
    [InlineData("Erneuerung_Neubau_m", "Sanierung")]
    [InlineData(FieldKeys.RehabilitationExecutor, "Sanierung")]
    [InlineData(FieldKeys.WorkflowStatus, "Sanierung")]
    // Kosten und Bemerkungen (3)
    [InlineData(FieldKeys.Cost, "Kosten und Bemerkungen")]
    [InlineData(FieldKeys.Link, "Kosten und Bemerkungen")]
    [InlineData(FieldKeys.Remarks, "Kosten und Bemerkungen")]
    // Bekannte Felder ausserhalb der vier Themenlisten und ein unbekanntes Feld
    [InlineData("Primaere_Schaeden", "Weitere Angaben")]
    [InlineData("NR", "Weitere Angaben")]
    [InlineData(FieldKeys.HierarchicalFunction, "Weitere Angaben")]
    [InlineData("Feld_Das_Nicht_Im_Katalog_Ist", "Weitere Angaben")]
    public void ResolveGroup_routes_known_fields(string fieldName, string expectedGroup)
    {
        Assert.Equal(expectedGroup, DataPageRecordDetailsBuilder.ResolveGroup(fieldName));
    }

    [Fact]
    public void Build_keeps_extra_fields_in_additional_group()
    {
        var record = new HaltungRecord();
        record.Fields["Z_Extra"] = "42";

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var additional = groups.Single(g => g.Title == "Weitere Angaben");

        Assert.Contains(additional.Items, item => item.Label == "Z_Extra");
    }

    [Fact]
    public void Build_emits_groups_in_stable_ui_order()
    {
        var record = new HaltungRecord();

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        Assert.Equal(new[]
        {
            "Stammdaten",
            "Bewertung",
            "Sanierung",
            "Kosten und Bemerkungen",
            "Weitere Angaben"
        }, groups.Select(g => g.Title));
        Assert.Equal(new[]
        {
            RecordDetailGroupKind.MasterData,
            RecordDetailGroupKind.Rating,
            RecordDetailGroupKind.Renovation,
            RecordDetailGroupKind.CostsRemarks,
            RecordDetailGroupKind.Additional
        }, groups.Select(g => g.Kind));
    }

    [Fact]
    public void Build_zeigt_Stammdaten_in_Prototyp_Reihenfolge()
    {
        var record = new HaltungRecord();
        record.Fields["Schacht_oben"] = "36262";
        record.Fields["Schacht_unten"] = "36275";
        var groups = DataPageRecordDetailsBuilder.Build(
            record, fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var stammdaten = groups.Single(g => g.Title == "Stammdaten");
        Assert.Equal(new[]
        {
            FieldKeys.HoldingName, FieldKeys.Street, "Schacht_oben", "Schacht_unten",
            FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm,
            FieldKeys.ProfileType, FieldKeys.ClearWidthMm, FieldKeys.UsageType, FieldKeys.HoldingLengthMeters,
            FieldKeys.SlopePromille,
            "Inspektionsrichtung", FieldKeys.InspectionYear, FieldKeys.ConstructionYear, FieldKeys.Owner,
            FieldKeys.GeonisId, FieldKeys.CadastreObjectId
        }, stammdaten.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_zeigt_Bewertung_in_Prototyp_Reihenfolge()
    {
        var record = new HaltungRecord();
        var groups = DataPageRecordDetailsBuilder.Build(
            record, fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var bewertung = groups.Single(g => g.Title == "Bewertung");
        Assert.Equal(new[]
        {
            FieldKeys.ConditionClass, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B",
            "VSA_Geschaetzt", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel"
        }, bewertung.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_zeigt_Sanierung_in_Prototyp_Reihenfolge()
    {
        var record = new HaltungRecord();
        var groups = DataPageRecordDetailsBuilder.Build(
            record, fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var sanierung = groups.Single(g => g.Title == "Sanierung");
        Assert.Equal(new[]
        {
            FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.LinerRenovationMeters,
            FieldKeys.LinerRenovationCount,
            FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair,
            "Erneuerung_Neubau_m", FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus
        }, sanierung.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_zeigt_Kosten_und_Bemerkungen_in_Prototyp_Reihenfolge()
    {
        var record = new HaltungRecord();
        var groups = DataPageRecordDetailsBuilder.Build(
            record, fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var kosten = groups.Single(g => g.Title == "Kosten und Bemerkungen");
        Assert.Equal(new[]
        {
            FieldKeys.Cost, FieldKeys.Link, FieldKeys.Remarks
        }, kosten.Items.Select(i => i.Label));
    }

    [Fact]
    public void Build_covers_every_catalog_field_exactly_once()
    {
        var record = new HaltungRecord();

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        // Label == Feldname (durch die Test-Factory oben).
        var renderedFields = groups
            .SelectMany(g => g.Items)
            .Select(item => item.Label)
            .ToList();

        // Jedes Katalogfeld erscheint genau einmal (faengt fehlend=0 und doppelt>1 ab).
        foreach (var field in FieldCatalog.ColumnOrder)
        {
            var count = renderedFields.Count(f => f == field);
            Assert.True(count == 1,
                $"Feld '{field}' sollte genau einmal editierbar erscheinen, war aber {count}x vorhanden.");
        }
    }
}
