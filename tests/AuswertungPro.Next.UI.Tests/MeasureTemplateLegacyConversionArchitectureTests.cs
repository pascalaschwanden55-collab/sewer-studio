using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MeasureTemplateLegacyConversionArchitectureTests
{
    [Fact]
    public void ViewModel_delegiert_reine_legacy_umwandlung_an_infrastructure()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "MeasureTemplateEditorViewModel.cs");
        var converterPath = RepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Costs",
            "LegacyMeasureTemplateConverter.cs");

        Assert.True(File.Exists(converterPath), "Die alte Vorlagenstruktur braucht einen eigenen reinen Konverter.");

        var viewModel = File.ReadAllText(viewModelPath);
        var converter = File.ReadAllText(converterPath);
        var compactViewModel = string.Concat(viewModel.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("LegacyMeasureTemplateConverter.Convert(legacy)", compactViewModel);
        Assert.DoesNotContain("ConvertLegacyTemplates", viewModel);
        Assert.DoesNotContain("ConvertLegacyTemplate", viewModel);
        Assert.DoesNotContain("ParseLegacyQtyOrDefault", viewModel);

        Assert.Contains("public static class LegacyMeasureTemplateConverter", converter);
        Assert.DoesNotContain("System.Windows", converter);
        Assert.DoesNotContain("IDialogService", converter);
        Assert.DoesNotContain("IMeasureTemplateStore", converter);
        Assert.DoesNotContain("File.", converter);
        Assert.DoesNotContain("JsonSerializer.Deserialize", converter);
    }

    [Fact]
    public void ViewModel_konvertiert_nach_dem_einlesen_und_vor_dem_laden_der_zieldatei()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "MeasureTemplateEditorViewModel.cs"));

        var migrationStart = viewModel.IndexOf("private void TryOfferLegacyMigration()", StringComparison.Ordinal);
        var migrationEnd = viewModel.IndexOf("private static decimal ParseQtyOrDefault", migrationStart, StringComparison.Ordinal);
        Assert.True(migrationStart >= 0 && migrationEnd > migrationStart);
        var migration = string.Concat(
            viewModel[migrationStart..migrationEnd].Where(character => !char.IsWhiteSpace(character)));
        var deserializeIndex = migration.IndexOf("JsonSerializer.Deserialize<LegacyMeasureTemplates>", StringComparison.Ordinal);
        var convertIndex = migration.IndexOf("LegacyMeasureTemplateConverter.Convert(legacy)", StringComparison.Ordinal);
        var loadOverridesIndex = migration.IndexOf("_templateStore.LoadUserOverrides()", StringComparison.Ordinal);

        Assert.True(
            deserializeIndex >= 0
            && deserializeIndex < convertIndex
            && convertIndex < loadOverridesIndex,
            "Einlesen, Umwandeln und Laden der Zieldatei muessen in dieser Reihenfolge bleiben.");
    }
}
