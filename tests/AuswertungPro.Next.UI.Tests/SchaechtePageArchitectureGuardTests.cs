using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageArchitectureGuardTests
{
    [Fact]
    public void SchaechtePage_dropdown_option_groups_live_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "SchaechtePageViewModel.cs");
        var controllerPath = Path.Combine(uiRoot, "Services", "DropdownOptionGroupController.cs");

        Assert.True(File.Exists(controllerPath), "Schaechte-Optionsgruppen sollen ausserhalb der ViewModel-Methoden orchestriert werden.");

        var viewModel = File.ReadAllText(viewModelPath);
        Assert.Contains("DropdownOptionGroupController", viewModel);
        Assert.DoesNotContain("new OptionsEditorWindow", viewModel);
        Assert.DoesNotContain("new OptionsEditorViewModel", viewModel);

        var removedMethodNames = new[]
        {
            "EditSanierenOptions",
            "PreviewSanierenOptions",
            "ResetSanierenOptions",
            "AddSanierenOption",
            "RemoveSanierenOption",
            "EditEigentuemerOptions",
            "PreviewEigentuemerOptions",
            "ResetEigentuemerOptions",
            "AddEigentuemerOption",
            "RemoveEigentuemerOption",
            "EditPruefungsresultatOptions",
            "PreviewPruefungsresultatOptions",
            "ResetPruefungsresultatOptions",
            "AddPruefungsresultatOption",
            "RemovePruefungsresultatOption",
            "EditReferenzpruefungOptions",
            "PreviewReferenzpruefungOptions",
            "ResetReferenzpruefungOptions",
            "AddReferenzpruefungOption",
            "RemoveReferenzpruefungOption"
        };

        foreach (var methodName in removedMethodNames)
            Assert.DoesNotContain($"private void {methodName}", viewModel);
    }

    [Fact]
    public void SchaechtePage_dropdown_record_sync_lives_in_synchronizer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "SchaechtePageViewModel.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Services", "SchaechteDropdownOptionSynchronizer.cs");

        Assert.True(File.Exists(synchronizerPath), "Schaechte-Dropdown-Sync aus Record-Feldern soll als testbarer Service existieren.");

        var viewModel = File.ReadAllText(viewModelPath);
        Assert.Contains("SchaechteDropdownOptionSynchronizer.SyncFromRecords", viewModel);
        Assert.DoesNotContain("private void SyncDropdownOptionsFromRecords", viewModel);
        Assert.DoesNotContain("private static string ResolveFieldValue", viewModel);
        Assert.DoesNotContain("private static string NormalizeKey", viewModel);
    }

    [Fact]
    public void SchaechtePage_template_column_reading_lives_in_infrastructure()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "SchaechtePageViewModel.cs");
        var infrastructurePath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Export",
            "Excel",
            "SchaechteTemplateColumnReader.cs");

        Assert.True(File.Exists(infrastructurePath), "Schaechte-Template-Spalten sollen ausserhalb der ViewModel-UI-Schicht gelesen werden.");

        var viewModel = File.ReadAllText(viewModelPath);
        Assert.Contains("SchaechteTemplateColumnReader.LoadFromExportDirectory", viewModel);
        Assert.DoesNotContain("using ClosedXML.Excel", viewModel);
        Assert.DoesNotContain("XLWorkbook", viewModel);
        Assert.DoesNotContain("private static string ResolveTemplatePath", viewModel);
        Assert.DoesNotContain("private void SwapColumnOrder", viewModel);
    }

    [Fact]
    public void SchaechtePage_search_and_nr_logic_uses_application_field_logic()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "SchaechtePageViewModel.cs");

        var viewModel = File.ReadAllText(viewModelPath);
        Assert.Contains("SchaechteFieldLogic.ResolveNrColumnName(Columns, Records)", viewModel);
        Assert.Contains("SchaechteFieldLogic.MatchesSearch(record, SearchText ?? \"\")", viewModel);
        Assert.Contains("SchaechteFieldLogic.BuildSearchResultInfo(visibleCount, Records.Count, SearchText ?? \"\")", viewModel);
        Assert.DoesNotContain("SchaechteSearchMatcher", viewModel);
        Assert.DoesNotContain("private string? ResolveNrColumnName()", viewModel);
        Assert.DoesNotContain("record.Fields.Any", viewModel);
        Assert.DoesNotContain("von {Records.Count} Schaechten", viewModel);
    }

    [Fact]
    public void SchaechtePage_schachtnummer_edit_uses_rename_service()
    {
        var root = FindRepositoryRoot();
        var pagePath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs");

        var page = File.ReadAllText(pagePath);

        Assert.Contains("private bool ApplySchachtNumberChange(", page);
        var method = ExtractMethod(page, "private bool ApplySchachtNumberChange(");
        Assert.Contains("ShaftRenameService.Rename(", method);
        Assert.Contains("record.SetFieldValue(\"Schachtnummer\"", method);
        Assert.Contains("PdfCorrectionMetadata.RegisterShaftRename", method);
    }

    [Fact]
    public void SchaechtePage_embeds_and_wires_schachtansicht()
    {
        var root = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("SchachtansichtView", pageXaml);
        Assert.Contains("SchachtansichtToggle_Changed", pageXaml);
        Assert.Contains("SchachtansichtView.DetailBuilder = BuildRecordDetailsForAnsicht", pageCode);
        Assert.Contains("SchachtansichtView.DamageLineBuilder = SchachtDamageLineBuilder.Build", pageCode);
        Assert.Contains("RouteSchachtansichtAction", pageCode);
    }

    [Fact]
    public void SchaechtePage_uses_zero_to_four_selection_for_zustandsklasse()
    {
        var root = FindRepositoryRoot();
        var pageCode = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));
        var schachtansichtXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "Schachtansicht",
            "SchachtansichtView.xaml"));

        var columnMethod = ExtractMethod(pageCode, "private DataGridColumn CreateZustandsklasseColumn(");
        Assert.Contains("DataGridComboBoxColumn", columnMethod);
        Assert.Contains("ZustandsklasseColorPalette.SelectionOptions", columnMethod);

        var detailMethod = ExtractMethod(pageCode, "private RecordDetailItem CreateSchachtDetailItem(");
        Assert.Contains("isCombo: true", detailMethod);
        Assert.Contains("allowFreeText: false", detailMethod);
        Assert.Contains("ZustandsklasseColorPalette.SelectionOptions", detailMethod);

        Assert.Contains("Zustand 0-4", schachtansichtXaml);
        Assert.Contains("ZustandsklasseValue_Click", schachtansichtXaml);
        Assert.Contains("ZkBrushConv", schachtansichtXaml);
    }
}
