using System.Linq;
using System.IO;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FieldKeysTests
{
    [Fact]
    public void Core_field_keys_match_persisted_project_field_names()
    {
        Assert.Equal("Haltungsname", FieldKeys.HoldingName);
        Assert.Equal("Strasse", FieldKeys.Street);
        Assert.Equal("Rohrmaterial", FieldKeys.PipeMaterial);
        Assert.Equal("DN_mm", FieldKeys.NominalDiameterMm);
        Assert.Equal("Nutzungsart", FieldKeys.UsageType);
        Assert.Equal("Haltungslaenge_m", FieldKeys.HoldingLengthMeters);
        Assert.Equal("Zustandsklasse", FieldKeys.ConditionClass);
        Assert.Equal("Sanieren_JaNein", FieldKeys.RenovationDecision);
        Assert.Equal(
            "Empfohlene_Sanierungsmassnahmen",
            FieldKeys.RecommendedRehabilitationMeasures);
        Assert.Equal("Kosten", FieldKeys.Cost);
        Assert.Equal("Eigentuemer", FieldKeys.Owner);
        Assert.Equal("Ausgefuehrt_durch", FieldKeys.RehabilitationExecutor);
        Assert.Equal("Offen_abgeschlossen", FieldKeys.WorkflowStatus);
        Assert.Equal("Datum_Jahr", FieldKeys.InspectionYear);
        Assert.Equal("Bemerkungen", FieldKeys.Remarks);
        Assert.Equal("Link", FieldKeys.Link);
        Assert.Equal("Renovierung_Inliner_Stk", FieldKeys.LinerRenovationCount);
        Assert.Equal("Renovierung_Inliner_m", FieldKeys.LinerRenovationMeters);
        Assert.Equal("Anschluesse_verpressen", FieldKeys.ConnectionsToGrout);
        Assert.Equal("Reparatur_Manschette", FieldKeys.RepairSleeve);
        Assert.Equal("Linerendmanschette_LEM", FieldKeys.LinerEndSleeve);
        Assert.Equal("Reparatur_Kurzliner", FieldKeys.ShortLinerRepair);
        Assert.Equal("Gefaelle_Promille", FieldKeys.SlopePromille);
        Assert.Equal("PDF_Path", FieldKeys.PdfPath);
        Assert.Equal("PDF_Eigen", FieldKeys.PdfEigen);
        Assert.Equal("PDF_All", FieldKeys.PdfAll);
    }

    [Fact]
    public void Core_field_catalog_contains_the_typed_persisted_keys()
    {
        string[] catalogKeys =
        [
            FieldKeys.HoldingName,
            FieldKeys.Street,
            FieldKeys.PipeMaterial,
            FieldKeys.NominalDiameterMm,
            FieldKeys.UsageType,
            FieldKeys.HoldingLengthMeters,
            FieldKeys.ConditionClass,
            FieldKeys.RenovationDecision,
            FieldKeys.RecommendedRehabilitationMeasures,
            FieldKeys.Cost,
            FieldKeys.Owner,
            FieldKeys.RehabilitationExecutor,
            FieldKeys.WorkflowStatus,
            FieldKeys.InspectionYear,
            FieldKeys.Remarks,
            FieldKeys.Link,
            FieldKeys.LinerRenovationCount,
            FieldKeys.LinerRenovationMeters,
            FieldKeys.ConnectionsToGrout,
            FieldKeys.RepairSleeve,
            FieldKeys.LinerEndSleeve,
            FieldKeys.ShortLinerRepair,
        ];

        foreach (var key in catalogKeys)
        {
            Assert.Contains(key, FieldCatalog.ColumnOrder);
            Assert.True(FieldCatalog.Definitions.ContainsKey(key), $"Felddefinition fehlt: {key}");
        }
    }

    [Theory]
    [InlineData("src", "AuswertungPro.Next.Application", "DataPage", "SanierungCostFieldMapper.cs")]
    [InlineData("src", "AuswertungPro.Next.Application", "DataPage", "DataPageHydraulikReportCalculator.cs")]
    [InlineData("src", "AuswertungPro.Next.Application", "Dashboard", "DashboardStatisticsBuilder.cs")]
    [InlineData("src", "AuswertungPro.Next.Infrastructure", "Costs", "MeasureImportDefaultsResolver.cs")]
    [InlineData("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SanierungsMatrixPageViewModel.cs")]
    [InlineData("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SanierungsMatrixNavigationTarget.cs")]
    [InlineData("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "BuilderPageRowBuilder.cs")]
    [InlineData("src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "CostCalculatorSummaryEntryBuilder.cs")]
    public void Refactored_cost_dashboard_and_hydraulics_paths_use_typed_field_keys(
        params string[] relativePath)
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile(relativePath));
        string[] typedKeys =
        [
            FieldKeys.HoldingName,
            FieldKeys.PipeMaterial,
            FieldKeys.NominalDiameterMm,
            FieldKeys.HoldingLengthMeters,
            FieldKeys.ConditionClass,
            FieldKeys.RenovationDecision,
            FieldKeys.RecommendedRehabilitationMeasures,
            FieldKeys.Cost,
            FieldKeys.Owner,
            FieldKeys.RehabilitationExecutor,
            FieldKeys.WorkflowStatus,
            FieldKeys.InspectionYear,
            FieldKeys.LinerRenovationCount,
            FieldKeys.LinerRenovationMeters,
            FieldKeys.ConnectionsToGrout,
            FieldKeys.RepairSleeve,
            FieldKeys.LinerEndSleeve,
            FieldKeys.ShortLinerRepair,
            FieldKeys.SlopePromille,
        ];

        foreach (var key in typedKeys)
        {
            Assert.DoesNotContain($"GetFieldValue(\"{key}\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain($"SetFieldValue(\"{key}\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain($"target[\"{key}\"]", source, StringComparison.Ordinal);
        }
    }

    // ═══════════════════════════════════════════════
    // Geschaetzte Zustandsnote sichtbar machen (Codeaudit 2026-08-17)
    //
    // Kann das Regelwerk einen Befund nicht benoten (fehlende Quantifizierung),
    // setzt VsaEvaluationService einen Standard-Schaetzwert je Schadenscode und
    // markiert das mit VSA_Geschaetzt. Das Feld stand aber weder in der
    // Spaltenreihenfolge noch im Export - einziger Leser war die
    // Schattenauswertung. In den Tabellen sah eine geschaetzte Note damit aus
    // wie eine gerechnete.
    //
    // Auf dem realen Bestand betrifft das 147 von 14'084 bewerteten Haltungen
    // (1,0 %). Fuer den Abgleich "passt meine Einschaetzung zum Code" sind
    // genau das die Faelle, in denen der Code gar nichts eingeschaetzt hat.
    // ═══════════════════════════════════════════════

    [Fact]
    public void VsaGeschaetzt_StehtInDerSpaltenreihenfolge()
    {
        Assert.Contains("VSA_Geschaetzt", FieldCatalog.ColumnOrder);
    }

    [Fact]
    public void VsaGeschaetzt_HatEineFeldbeschreibung()
    {
        var feld = Assert.Single(
            FieldCatalog.Definitions.Values.Where(d => d.Name == "VSA_Geschaetzt"));
        Assert.False(string.IsNullOrWhiteSpace(feld.Label));
    }

    [Fact]
    public void VsaGeschaetzt_StehtNebenDenZustandsnoten()
    {
        // Nuetzt nur, wenn es dort auftaucht, wo die Noten gelesen werden.
        var reihenfolge = FieldCatalog.ColumnOrder;
        var geschaetzt = reihenfolge.ToList().IndexOf("VSA_Geschaetzt");
        var noteB = reihenfolge.ToList().IndexOf("VSA_Zustandsnote_B");
        Assert.True(geschaetzt >= 0 && noteB >= 0);
        Assert.Equal(noteB + 1, geschaetzt);
    }
}
