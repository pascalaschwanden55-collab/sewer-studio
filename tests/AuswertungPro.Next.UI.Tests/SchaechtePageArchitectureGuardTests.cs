using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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
        AssertNoForbiddenTokens(
            viewModel,
            "new OptionsEditorWindow",
            "new OptionsEditorViewModel");

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
            AssertNoForbiddenTokens(viewModel, $"private void {methodName}");
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
        AssertNoForbiddenTokens(
            viewModel,
            "private void SyncDropdownOptionsFromRecords",
            "private static string ResolveFieldValue",
            "private static string NormalizeKey");
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
        Assert.Contains("_templateColumnReader.LoadFromExportDirectory", viewModel);
        Assert.DoesNotContain("SchaechteTemplateColumnReader.LoadFromExportDirectory", viewModel);
        AssertNoForbiddenTokens(
            viewModel,
            "using ClosedXML.Excel",
            "XLWorkbook",
            "private static string ResolveTemplatePath",
            "private void SwapColumnOrder");
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
        AssertNoForbiddenTokens(
            viewModel,
            "SchaechteSearchMatcher",
            "private string? ResolveNrColumnName()",
            "record.Fields.Any",
            "von {Records.Count} Schaechten");
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
        var controllerPath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "SchaechteShaftRenameController.cs");

        var page = File.ReadAllText(pagePath);
        Assert.True(File.Exists(controllerPath), controllerPath);
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("private bool ApplySchachtNumberChange(", page);
        Assert.Contains("SchaechteShaftRenameController.Apply(", page);
        Assert.Contains("Vm.ShaftRename", page);
        Assert.Contains("IShaftRenameService renameService", controller);
        Assert.Contains("renameService.Rename(", controller);
        Assert.DoesNotContain("ShaftRenameService.Rename(", controller);
        Assert.Contains("record.SetFieldValue(\"Schachtnummer\"", controller);
        Assert.Contains("PdfCorrectionMetadata.RegisterShaftRename", controller);

        var provider = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ServiceProvider.cs"));
        Assert.Contains("public IShaftRenameService ShaftRename { get; }", provider);
        Assert.Contains("ShaftRename = new ShaftRenameFileService();", provider);
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
        var detailsBuilderPath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "SchaechteRecordDetailsBuilder.cs");
        Assert.True(File.Exists(detailsBuilderPath), detailsBuilderPath);
        var detailsBuilder = File.ReadAllText(detailsBuilderPath);
        var schachtansichtXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "Schachtansicht",
            "SchachtansichtView.xaml"));

        Assert.Contains("private DataGridColumn CreateZustandsklasseColumn(", pageCode);
        Assert.Contains("DataGridComboBoxColumn", pageCode);
        Assert.Contains("ZustandsklasseColorPalette.SelectionOptions", pageCode);
        Assert.Contains("SchaechteRecordDetailsBuilder", pageCode);
        Assert.Contains("private RecordDetailItem CreateItem(", detailsBuilder);
        Assert.Contains("isCombo: true", detailsBuilder);
        Assert.Contains("allowFreeText: false", detailsBuilder);

        Assert.Contains("Zustand 0-4", schachtansichtXaml);
        Assert.Contains("ZustandsklasseValue_Click", schachtansichtXaml);
        Assert.Contains("ZkBrushConv", schachtansichtXaml);
    }

    [Fact]
    public void SchaechtePage_context_menus_can_reveal_schacht_folder()
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
        var schachtansichtXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "Schachtansicht",
            "SchachtansichtView.xaml"));
        var schachtansichtCode = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "Schachtansicht",
            "SchachtansichtView.xaml.cs"));

        Assert.Contains("Header=\"Gehe zu Ordner\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenContainingFolderMenu_Click\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Gehe zu Ordner\"", schachtansichtXaml, StringComparison.Ordinal);
        Assert.Contains("CtxOpenFolder_Click", schachtansichtXaml, StringComparison.Ordinal);
        Assert.Contains("RaiseAction(\"openfolder\")", schachtansichtCode, StringComparison.Ordinal);
        Assert.Contains("case \"openfolder\":", pageCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SchaechtePage_massnahmen_dialog_and_persistence_live_in_controller()
    {
        var root = FindRepositoryRoot();
        var pagePath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs");
        var controllerPath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "Schachtansicht",
            "SchachtMassnahmenDialogController.cs");

        Assert.True(File.Exists(controllerPath), controllerPath);
        var page = File.ReadAllText(pagePath);
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("_massnahmenController?.Open(record)", page);
        AssertNoForbiddenTokens(
            page,
            "ProjectCostStoreRepository",
            "new SchachtMassnahmenWindow",
            "new SchachtMassnahmenKatalogEditorWindow");

        Assert.Contains("IProjectCostStoreRepository repository", controller);
        Assert.DoesNotContain("new ProjectCostStoreRepository", controller);
        Assert.Contains("store.ByHolding[schachtNummer] = cost", controller);
        Assert.Contains("new SchachtMassnahmenWindow", controller);
        Assert.Contains("new SchachtMassnahmenKatalogEditorWindow", controller);
        Assert.DoesNotContain("ServiceProvider", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_services", controller, StringComparison.Ordinal);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte SchaechtePage-Logik gefunden: " + string.Join(", ", hits));
    }
}
