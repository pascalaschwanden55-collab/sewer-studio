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
        var factoryPath = Path.Combine(uiRoot, "Services", "SchaechteDropdownCommandFactory.cs");

        Assert.True(File.Exists(controllerPath), "Schaechte-Optionsgruppen sollen ausserhalb der ViewModel-Methoden orchestriert werden.");
        Assert.True(File.Exists(factoryPath), "Schaechte-Dropdown-Gruppen und Commands sollen gemeinsam erzeugt werden.");

        var viewModel = File.ReadAllText(viewModelPath);
        var factory = File.ReadAllText(factoryPath);
        var compactViewModel = string.Concat(viewModel.Where(character => !char.IsWhiteSpace(character)));
        Assert.Contains("SchaechteDropdownCommandFactory.Create", viewModel);
        Assert.DoesNotContain("DropdownOptionGroupController", viewModel, StringComparison.Ordinal);
        Assert.Contains("DropdownOptionGroupController", factory);
        Assert.Contains("DropdownCommandFactory.Create", factory);
        Assert.Contains(
            "_dropdownCommands=SchaechteDropdownCommandFactory.Create(" +
            "newSchaechteDropdownOptionCollections(" +
            "SanierenOptions,EigentuemerOptions,PruefungsresultatOptions,ReferenzpruefungOptions)," +
            "_dropdownOptions.FixedEigentuemerOptions," +
            "newDropdownOptionGroupActions(" +
            "OptionsEditorDialogService.Show,_dialogs.Info,SaveDropdownOptions));",
            compactViewModel);

        var commandGroups = new[]
        {
            (PropertyName: "Sanieren", GroupName: "Sanieren"),
            (PropertyName: "Eigentuemer", GroupName: "Eigentuemer"),
            (PropertyName: "Pruefungsresultat", GroupName: "Pruefungsresultat"),
            (PropertyName: "Referenzpruefung", GroupName: "Referenzpruefung")
        };

        foreach (var (propertyName, groupName) in commandGroups)
        {
            foreach (var actionName in new[] { "Edit", "Preview", "Reset" })
            {
                Assert.Contains(
                    $"publicIRelayCommand{actionName}{propertyName}OptionsCommand=>" +
                    $"_dropdownCommands.{groupName}.{actionName};",
                    compactViewModel);
            }

            foreach (var actionName in new[] { "Add", "Remove" })
            {
                Assert.Contains(
                    $"publicIRelayCommand<object?>{actionName}{propertyName}OptionCommand=>" +
                    $"_dropdownCommands.{groupName}.{actionName};",
                    compactViewModel);
            }
        }

        AssertNoForbiddenTokens(
            viewModel,
            "_sanierenDropdownOptions",
            "_eigentuemerDropdownOptions",
            "_pruefungsresultatDropdownOptions",
            "_referenzpruefungDropdownOptions",
            "private DropdownOptionGroupController CreateDropdownOptionGroup",
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
        var recordPartialPath = Path.Combine(
            uiRoot,
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.RecordCollection.cs");
        var recordControllerPath = Path.Combine(
            uiRoot,
            "DataPage",
            "SchaechteRecordCollectionController.cs");

        var viewModel = File.ReadAllText(viewModelPath);
        Assert.True(File.Exists(recordPartialPath), recordPartialPath);
        Assert.True(File.Exists(recordControllerPath), recordControllerPath);
        var recordPartial = File.ReadAllText(recordPartialPath);
        var recordController = File.ReadAllText(recordControllerPath);
        Assert.Contains("SchaechteFieldLogic.ResolveNrColumnName(", recordController);
        Assert.Contains("SchaechteRecordCollectionController", recordPartial);
        Assert.DoesNotContain("Records.Add(", viewModel);
        Assert.DoesNotContain("Records.RemoveAt(", viewModel);
        Assert.DoesNotContain("Records.Move(", viewModel);
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
        Assert.Contains("Vm.PdfTextLayerRewrite", page);
        Assert.Contains("IShaftRenameService renameService", controller);
        Assert.Contains("IPdfTextLayerRewriter pdfTextLayerRewrite", controller);
        Assert.Contains("renameService.Rename(", controller);
        Assert.Contains("pdfTextLayerRewrite.RewriteIdentifierInPlace(", controller);
        Assert.DoesNotContain("ShaftRenameService.Rename(", controller);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure.HoldingFolderDistributor", controller);
        Assert.Contains("record.SetFieldValue(\"Schachtnummer\"", controller);
        Assert.Contains("PdfCorrectionMetadata.RegisterShaftRename", controller);

        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.cs"));
        Assert.Contains("private readonly IPdfTextLayerRewriter _pdfTextLayerRewrite;", viewModel);
        Assert.Contains("pdfTextLayerRewrite: services.PdfTextLayerRewrite", viewModel);
        Assert.Contains("_pdfTextLayerRewrite = pdfTextLayerRewrite ?? throw", viewModel);
        Assert.DoesNotContain("_pdfTextLayerRewrite = pdfTextLayerRewrite ?? PdfTextLayerRewriter.Current", viewModel);

        var provider = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ServiceProvider.cs"));
        Assert.Contains("public IShaftRenameService ShaftRename { get; }", provider);
        Assert.Contains("ShaftRename = new ShaftRenameFileService();", provider);
        Assert.Contains("public IPdfTextLayerRewriter PdfTextLayerRewrite { get; }", provider);
    }

    [Fact]
    public void SchaechtePage_simple_field_edit_logic_lives_in_controller()
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
            "SchaechteFieldEditController.cs");

        Assert.True(File.Exists(controllerPath), controllerPath);
        var page = File.ReadAllText(pagePath);
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("SchaechteFieldEditController.Apply(", page);
        Assert.DoesNotContain("record.SetFieldValue(recordField", page);
        // Handeingabe wird ausdruecklich als solche geschrieben, damit automatische
        // Schreiber sie nicht ueberholen (siehe SchachtRecordFieldProtectionTests).
        Assert.Contains(
            "record.SetFieldValue(fieldName, editedValue, FieldSource.Manual, userEdited: true);",
            controller);
        Assert.Contains("SchaechteColumnPolicy.ResolveOptionField(fieldName)", controller);
        Assert.Contains("applyShaftNumberChange(record, oldShaftNumber, editedValue)", controller);
    }

    [Fact]
    public void SchaechtePage_reuses_shared_grid_zoom_controller()
    {
        var page = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("DataPageGridZoomController.Resolve(", page);
        Assert.DoesNotContain("const double step = 0.05d;", page);
        Assert.DoesNotContain("Math.Clamp(vm.GridZoom + delta, 0.5d, 2.0d)", page);
    }

    [Fact]
    public void SchaechtePage_protocol_folder_summary_and_folder_rules_live_in_policy()
    {
        var viewModelPartial = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.ProtocolFolderImport.cs"));
        var protocolEntryPartial = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.ProtocolImport.cs"));
        var policyPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "SchachtProtocolFolderImportPolicy.cs");

        Assert.True(File.Exists(policyPath), policyPath);
        var policy = File.ReadAllText(policyPath);
        var compactViewModelPartial = string.Concat(
            viewModelPartial.Where(character => !char.IsWhiteSpace(character)));
        var compactProtocolEntryPartial = string.Concat(
            protocolEntryPartial.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(", viewModelPartial);
        Assert.Contains("SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(", viewModelPartial);
        Assert.Contains(
            "SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(" +
            "sourcePdfs.Count,preparedPdfs.Length,created,updated," +
            "skippedOlderPdfCandidates,skippedDirectories.Count,failures)",
            compactViewModelPartial);
        Assert.Contains(
            "SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(" +
            "pdfPath,parsed.Schachtnummer,existingShaftNumbers," +
            "destinationFolder,legacyDestinationFolder)",
            compactViewModelPartial);
        Assert.DoesNotContain("private static string BuildFolderImportSummary(", viewModelPartial);
        Assert.DoesNotContain("private static string? ResolveCanonicalShaftFolder(", viewModelPartial);
        Assert.DoesNotContain("private static bool PathsEqual(", viewModelPartial);
        Assert.Contains("internal static string BuildFolderImportSummary(", policy);
        Assert.Contains("internal static string? ResolveCanonicalShaftFolder(", policy);
        Assert.Contains("failures.Take(8)", policy);
        Assert.Contains("Path.TrimEndingDirectorySeparator", policy);
        Assert.Contains("project: expectedProject", viewModelPartial);
        Assert.DoesNotContain("project: _shell.Project", viewModelPartial);
        Assert.Contains(
            "ProjectOperationImpact.ProjectFilesWritten",
            viewModelPartial);
        Assert.Contains(
            "var distributionImpact = !readsExistingDistribution && preparedPdfs.Length > 0",
            viewModelPartial);
        Assert.Contains(
            "distributionImpact | ProjectOperationImpact.ProjectDataChanged",
            viewModelPartial);
        Assert.Contains(
            "varprojectContext=newProjectOperationContext(" +
            "_shell.Project,_settings.LastProjectPath);",
            compactProtocolEntryPartial);
        Assert.Contains(
            "ImportProtocolFolderAsync(projectContext,projektOrdner,ordner)",
            compactProtocolEntryPartial);
        var projectGuards = new List<int>();
        var guardSearchStart = 0;
        while (true)
        {
            var guard = compactViewModelPartial.IndexOf(
                "ProjectIsStillOpen(projectContext",
                guardSearchStart,
                StringComparison.Ordinal);
            if (guard < 0)
                break;
            projectGuards.Add(guard);
            guardSearchStart = guard + 1;
        }

        Assert.Equal(7, projectGuards.Count);
        Assert.Contains("ActiveProjectGuard.IsCurrent(", viewModelPartial);
        Assert.Contains("targetRemovedBeforeApply", viewModelPartial);
        var confirmation = compactViewModelPartial.IndexOf(
            "ConfirmWarn(",
            projectGuards[0],
            StringComparison.Ordinal);
        var restorePoint = compactViewModelPartial.IndexOf(
            "TryCreateImportRestorePoint",
            StringComparison.Ordinal);
        var distribute = compactViewModelPartial.IndexOf(
            "DistributeShaftFiles",
            StringComparison.Ordinal);
        var parse = compactViewModelPartial.IndexOf(
            "_schachtProtocolImport.Parse(pdfPath)",
            StringComparison.Ordinal);
        var apply = compactViewModelPartial.IndexOf(
            "_schachtProtocolImport.Apply(target",
            StringComparison.Ordinal);
        var immediateApplyGuard = compactViewModelPartial.IndexOf(
            "ActiveProjectGuard.IsCurrent(projectContext",
            StringComparison.Ordinal);
        var markDirty = compactViewModelPartial.IndexOf(
            "expectedProject.Dirty=true",
            StringComparison.Ordinal);
        var select = compactViewModelPartial.IndexOf(
            "Selected=lastTarget",
            StringComparison.Ordinal);
        var save = compactViewModelPartial.IndexOf(
            "_saveProjectForProtocolImport()",
            StringComparison.Ordinal);
        Assert.True(
            projectGuards[0] < confirmation
            && confirmation < projectGuards[1]
            && projectGuards[1] < restorePoint);
        Assert.True(
            restorePoint < distribute
            && distribute < projectGuards[2]
            && projectGuards[2] < parse
            && parse < projectGuards[3]
            && projectGuards[3] < immediateApplyGuard
            && immediateApplyGuard < apply
            && apply < projectGuards[4]
            && projectGuards[4] < markDirty
            && markDirty < projectGuards[5]
            && projectGuards[5] < select
            && select < projectGuards[6]
            && projectGuards[6] < save);
    }

    [Fact]
    public void SchaechtePage_stammdaten_result_application_lives_in_applier()
    {
        var viewModelPartial = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.Stammdaten.cs"));
        var applierPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "SchachtStammdatenResultApplier.cs");

        Assert.True(File.Exists(applierPath), applierPath);
        var applier = File.ReadAllText(applierPath);
        var compactViewModelPartial = string.Concat(
            viewModelPartial.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("SchachtStammdatenResultApplier.Apply(", viewModelPartial);
        Assert.Contains("beforeApply:", viewModelPartial);
        Assert.Contains(
            "SchachtStammdatenResultApplier.Apply(projectRecords,result,beforeApply:()=>",
            compactViewModelPartial);
        Assert.Contains(
            "varapplyResult=SchachtStammdatenResultApplier.Apply(" +
            "projectRecords,result,beforeApply:()=>{" +
            "if(result.Ergaenzungen.Count>0)" +
            "_shell.TryCreateImportRestorePoint(\"Schacht-PDF-Stammdaten\");});",
            compactViewModelPartial);
        Assert.Contains(
            "if(result.Ergaenzungen.Count>0)_shell.TryCreateImportRestorePoint(" +
            "\"Schacht-PDF-Stammdaten\")",
            compactViewModelPartial);
        Assert.Contains("_shell.TryCreateImportRestorePoint(\"Schacht-PDF-Stammdaten\")", viewModelPartial);
        Assert.Contains("if (applyResult.ChangedShaftCount > 0)", viewModelPartial);
        Assert.Contains("varproject=projectContext.Project;", compactViewModelPartial);
        Assert.Contains("project.ModifiedAtUtc=DateTime.UtcNow;", compactViewModelPartial);
        Assert.Contains("project.Dirty=true;", compactViewModelPartial);
        Assert.Contains("ProjectOperationImpact.ProjectDataChanged", viewModelPartial);
        Assert.Contains("_shell.MarkProjectDirty()", viewModelPartial);
        Assert.Contains("_saveProjectForProtocolImport()", viewModelPartial);
        Assert.DoesNotContain("_shell.TrySaveProject()", viewModelPartial);
        Assert.Contains("TryBeginProtocolPdfOperation", viewModelPartial);
        Assert.Contains("EndProtocolPdfOperation", viewModelPartial);
        Assert.Contains("ProjectFileLocator.ProjectRootFromFile(projectContext.ProjectPath)", viewModelPartial);
        Assert.Contains("projectContext.Project.SchaechteData", viewModelPartial);
        Assert.Contains("_dialogs.Info(applyResult.DialogText", viewModelPartial);
        Assert.Contains("LastResult=applyResult.Summary;", compactViewModelPartial);
        Assert.Contains("StammdatenErgaenzungText=applyResult.Summary;", compactViewModelPartial);
        Assert.Contains(
            "conststringdialogTitle=\"PDF-Stammdatenergaenzen\";",
            compactViewModelPartial);
        Assert.Contains(
            "_dialogs.Info(applyResult.DialogText,dialogTitle)",
            compactViewModelPartial);
        Assert.DoesNotContain("Records.ToDictionary", viewModelPartial);
        Assert.DoesNotContain("private static bool SetIfMissing(", viewModelPartial);
        Assert.DoesNotContain("result.Meldungen.Take(12)", viewModelPartial);
        Assert.Contains("records.ToDictionary", applier);
        Assert.Contains("beforeApply?.Invoke()", applier);
        Assert.Contains("private static bool SetIfMissing(", applier);
        Assert.Contains("result.Meldungen.Take(12)", applier);
        Assert.DoesNotContain("_shell", applier);
        Assert.DoesNotContain("_dialogs", applier);
    }

    [Fact]
    public void SchaechtePage_linked_protocol_refresh_lives_in_controller()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.cs");
        var partialPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.ProtocolImport.cs");
        var controllerPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "SchachtProtocolRefreshController.cs");
        var projectGuardPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "ActiveProjectGuard.cs");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.True(File.Exists(projectGuardPath), projectGuardPath);
        var viewModel = File.ReadAllText(viewModelPath);
        var partial = File.ReadAllText(partialPath);
        var controller = File.ReadAllText(controllerPath);
        var projectGuard = File.ReadAllText(projectGuardPath);
        var compactViewModel = string.Concat(viewModel.Where(character => !char.IsWhiteSpace(character)));
        var compactPartial = string.Concat(partial.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("private readonly SchachtProtocolRefreshController", viewModel);
        Assert.Contains("new SchachtProtocolRefreshActions(", viewModel);
        Assert.Contains(
            "_schachtProtocolRefreshController=newSchachtProtocolRefreshController(" +
            "_dialogs,newSchachtProtocolRefreshActions(" +
            "GetProjectFolder:_shell.GetProjectFolder," +
            "CaptureProject:()=>newProjectOperationContext(" +
            "_shell.Project,_settings.LastProjectPath)," +
            "LocateProtocolFile:LocateProtocolFile," +
            "ReadProtocolAsync:ReadProtocolAsync," +
            "ProjectIsStillOpen:ProjectIsStillOpen," +
            "Apply:RebuildFromProtocol," +
            "SaveProject:_saveProjectForProtocolImport," +
            "SetLastResult:value=>LastResult=value));",
            compactViewModel);
        // Aktualisieren baut genau diesen einen Schacht komplett aus dem frisch
        // gelesenen Protokoll neu auf; der ergaenzende Import bleibt davon getrennt.
        Assert.Contains(
            "if(_schachtProtocolImportisISchachtProtocolRebuildServicerebuild)" +
            "rebuild.Rebuild(schacht,protokoll,pdfPfadFuerFeld);",
            compactPartial);
        Assert.Contains("CanStartProtocolPdfOperation()", partial);
        Assert.Contains("SchachtProtocolRefreshController.CanExecute(Selected)", partial);
        Assert.Contains(
            "privateasyncTaskRefreshProtocolAsync(){" +
            "if(!TryBeginProtocolPdfOperation(\"Protokollaktualisierung\"))return;" +
            "try{_=await_schachtProtocolRefreshController.ExecuteAsync(Selected);}" +
            "finally{EndProtocolPdfOperation();}}",
            compactPartial);
        Assert.Contains("_actions.Apply(selected, result, pathForRecord)", controller);
        // Die Dateisuche selbst bleibt im injizierten Locator; der Controller entscheidet nur.
        Assert.Contains("_actions.LocateProtocolFile(selected, projectFolder)", controller);
        Assert.Contains("_protocolFileLocator.Locate(", partial);
        Assert.Contains("if (!ProjectSaveAttempt.Try(", controller);
        Assert.Contains("_actions.SaveProject,", controller);
        Assert.Contains("ProjectSaveAttempt.ErrorDetails(saveError)", controller);
        Assert.Contains("SchachtProtocolRefreshOutcome.UpdatedButNotSaved", controller);
        Assert.Contains("ActiveProjectGuard.IsCurrent(", partial);
        Assert.Contains("ReferenceEquals(expected.Project, currentProject)", projectGuard);
        Assert.Contains("_settings.LastProjectPath", partial);
        Assert.DoesNotContain("ServiceProvider", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void SchaechtePage_single_protocol_import_lives_in_controller()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.cs");
        var partialPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.ProtocolImport.cs");
        var controllerPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "SchachtProtocolSingleImportController.cs");

        Assert.True(File.Exists(controllerPath), controllerPath);
        var viewModel = File.ReadAllText(viewModelPath);
        var partial = File.ReadAllText(partialPath);
        var controller = File.ReadAllText(controllerPath);
        var compactViewModel = string.Concat(viewModel.Where(character => !char.IsWhiteSpace(character)));
        var compactPartial = string.Concat(partial.Where(character => !char.IsWhiteSpace(character)));
        var compactController = string.Concat(controller.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("private readonly SchachtProtocolSingleImportController", viewModel);
        Assert.Contains(
            "_schachtProtocolSingleImportController=newSchachtProtocolSingleImportController(" +
             "_dialogs,_schachtProtocolImport,newSchachtProtocolSingleImportActions(" +
            "ReadProtocolAsync:ReadProtocolAsync," +
            "ProjectIsStillOpen:ProjectIsStillOpen," +
            "CollectionLock:_shell.CollectionLock," +
            "SaveProject:_saveProjectForProtocolImport," +
            "SetSelected:record=>Selected=record," +
            "ClearSelectedIfSame:ClearSelectedIfSame," +
            "SetLastResult:value=>LastResult=value));",
            compactViewModel);
        Assert.Contains(
            "privateTaskImportSingleProtocolAsync(" +
            "ProjectOperationContextprojectContext,stringprojektOrdner,stringpdfPfad)" +
            "=>_schachtProtocolSingleImportController.ExecuteAsync(" +
            "projectContext,projektOrdner,pdfPfad);",
            compactPartial);
        Assert.Contains("_protocolImport.FindSchacht(", controller);
        Assert.Contains("_protocolImport.DistributePdf(", controller);
        Assert.Contains("_protocolImport.Apply(target, result, distribution.RelativePath)", controller);
        Assert.Contains("lock (_actions.CollectionLock)", controller);
        Assert.Contains("RequiresProjectMembership", controller);
        Assert.Contains("targetRemoved", controller);
        Assert.Contains("Der geloeschte Datensatz wurde nicht wieder eingefuegt", controller);
        Assert.Contains(
            "distribution=awaitTask.Run(()=>DistributePdf(" +
            "projectFolder,result.Schachtnummer,pdfPath));",
            compactController);
        Assert.Contains(
            "project.ModifiedAtUtc=DateTime.UtcNow;" +
            "project.Dirty=true;" +
            "varcommittedImpact=fileImpact|ProjectOperationImpact.ProjectDataChanged;" +
            "if(!_actions.ProjectIsStillOpen(" +
            "projectContext,DialogTitle,committedImpact)){return;}" +
            "_actions.SetSelected(target);" +
            "if(!_actions.ProjectIsStillOpen(" +
            "projectContext,DialogTitle,committedImpact)){" +
            "_actions.ClearSelectedIfSame(target);return;}" +
            "varsaved=ProjectSaveAttempt.Try(" +
            "_actions.SaveProject," +
            "\"ImportiertesSchachtprotokollspeichern\"," +
            "outvarsaveError);",
            compactController);
        Assert.Contains("uebernommen, aber nicht gespeichert", controller);
        Assert.Contains("ProjectSaveAttempt.ErrorDetails(saveError)", controller);
        Assert.Contains(
            "if(ReferenceEquals(Selected,expectedSelection))Selected=null;",
            compactPartial);
        AssertNoForbiddenTokens(
            partial,
            "private SchachtRecord? ResolveProtocolTarget(",
            "_schachtProtocolImport.DistributePdf(",
            "Records.Contains(ziel)",
            "Records.Add(ziel)",
            "_schachtProtocolImport.Apply(ziel",
            "Schacht {ergebnis.Schachtnummer} ist bereits vorhanden.");
        Assert.DoesNotContain("ServiceProvider", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", controller, StringComparison.Ordinal);
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
    public void SchaechtePage_uses_field_selection_without_duplicate_zustandsklasse_bar()
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

        Assert.DoesNotContain("Zustand 0-4", schachtansichtXaml);
        Assert.DoesNotContain("ZustandsklasseValue_Click", schachtansichtXaml);
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
