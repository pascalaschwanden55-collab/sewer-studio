using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterBatchImportArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_batch_cancel_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunControlController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Trivialer Batch-Cancel soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportRunControlController.RequestCancel", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_genCts?.Cancel();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Abbruch angefordert...\";", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportTerminalPresentationBuilder.BuildCancelRequestedStatus", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_batch_import_run_preparation_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunPreparationController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Batch-Import-Run-Preparation soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportRunPreparationController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("if (IsBusy) return;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_rootFolders.Count", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Bitte zuerst einen oder mehrere Ordner wählen.\";", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_genCts?.Cancel();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_genCts?.Dispose();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var runCts = new CancellationTokenSource();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("runCts.Dispose();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_genCts = runCts;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var ct = runCts.Token;", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_startzustand_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunStartController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Trivialer Batch-Startzustand soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportRunStartController.Apply(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetBusy(true);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetLogText(\"\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetProgressValue(0);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetProgressMax(1);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ClearLivePreview();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ResetSelfTrainingVisuals();", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_auto_approve_bestaetigung_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportAutoApproveConfirmationController.Confirm(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("DialogHost.Current);", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var bestaetigung = DialogHost.Current.ConfirmWarn(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Trotzdem unge", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch-Import + KB (", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_batch_import_fehlerbehandlung_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunExceptionController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Batch-Import-Fehlerbehandlung soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportRunExceptionController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("runSummary.RecordError(ex.Message);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.Log($\"  FEHLER: {ex.Message}\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.Log(\"Batch-Import abgebrochen durch Benutzer.\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetStatusText(\"Batch-Import abgebrochen.\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.Log($\"FATALER FEHLER: {ex.Message}\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetStatusText($\"Fehler beim Batch-Import: {ex.Message}\");", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_abschluss_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportRunCompletionController.CompleteAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Samples", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.BuildNoNewStatus(casesToProcess.Count)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.BuildCompletionStatus()", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Samples.Clear", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Samples.Add", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(\"F", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_batch_import_final_state_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("batchUi.SetBusy(false);", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportRunFinalizerController.Apply(", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_scan_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportScanWorkflowController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Clear();", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("STOP: Keine Ordner mit Protokoll-Dateien gefunden.", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportScanPresentationBuilder.BuildSummary(found.Count, casesWithProtocol.Count)", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_runtime_setup_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportRuntimeSetupController.PrepareAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var allSamples = await TrainingSamplesStore.LoadAsync();", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var existingSigs = allSamples.Select", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var casesToProcess = casesWithProtocol;", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var runSummary = new TrainingBatchImportRunSummary();", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingBatchImportRuntimeSetupController_setzt_existing_sample_snapshot_inline()
    {
        var repoRoot = FindRepoRoot();
        var snapshotControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportExistingSampleSnapshotController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRuntimeSetupController.cs"));
        var controllerSource = source;

        Assert.False(File.Exists(snapshotControllerPath), snapshotControllerPath);
        Assert.DoesNotContain("TrainingBatchImportExistingSampleSnapshotController", source, StringComparison.Ordinal);
        Assert.Contains("var allSamples = await loadSamplesAsync().ConfigureAwait(false);", controllerSource, StringComparison.Ordinal);
        Assert.Contains("var existingSigs = allSamples.Select(s => s.Signature)", controllerSource, StringComparison.Ordinal);
        Assert.Contains("ToHashSet(StringComparer.Ordinal)", controllerSource, StringComparison.Ordinal);
        Assert.Contains("log($\"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)\");", controllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_generated_case_ui_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var candidateWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseCandidateWorkflowController.cs"));
        var caseWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseWorkflowController.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseCandidateWorkflowController.Apply(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportGeneratedCaseUiController.Apply(", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var plan in generatedCasePlan.SampleUiPlans)", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.AddNewSamples(generatedCasePlan.NewSampleCount)", candidateWorkflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingBatchImportCaseWorkflowController_buendelt_case_ui_delegates_in_sink()
    {
        var caseWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseWorkflowController.cs"));
        var candidateWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseCandidateWorkflowController.cs"));
        var generatedCaseUiSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportGeneratedCaseUiController.cs"));
        var persistenceWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCasePersistenceWorkflowController.cs"));

        Assert.Contains("TrainingBatchImportCaseUiSink caseUi", caseWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseUiSink caseUi", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseUiSink caseUi", generatedCaseUiSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseUiSink caseUi", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<TrainingBatchImportLivePreview> updateLivePreview", caseWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<Action> invokeOnUi", caseWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<SelfTrainingEntryResult> addResult", caseWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<string, MatchLevel> updateCodeDistribution", caseWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<Action> invokeOnUi", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<int> setSampleCount", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<int> setCodesCovered", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetSampleCount(persistence.SampleCount);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetCodesCovered(persistence.CodesCovered);", persistenceWorkflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingBatchImportGeneratedCaseController_setzt_triviale_sample_log_zeilen_inline()
    {
        var generatedCaseControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportGeneratedCaseController.cs");
        var sampleLogBuilderPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportSampleLogBuilder.cs");
        var generatedCaseControllerSource = File.ReadAllText(generatedCaseControllerPath);

        Assert.False(File.Exists(sampleLogBuilderPath), "Triviale Sample-Log-Zeilen sollen im Generated-Case-Controller stehen.");
        Assert.DoesNotContain("TrainingBatchImportSampleLogBuilder", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("\"  -> {samples.Count} Samples", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("\"     {sample.Code} @ {sample.MeterStart:F2}m", generatedCaseControllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingBatchImportGeneratedCaseController_setzt_triviale_skip_case_ui_planung_inline()
    {
        var generatedCaseControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportGeneratedCaseController.cs");
        var skippedCaseUiPlanBuilderPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportSkippedCaseUiPlanBuilder.cs");
        var generatedCaseControllerSource = File.ReadAllText(generatedCaseControllerPath);

        Assert.False(File.Exists(skippedCaseUiPlanBuilderPath), "Triviale Skip-Case-UI-Planung soll im Generated-Case-Controller stehen.");
        Assert.DoesNotContain("TrainingBatchImportSkippedCaseUiPlanBuilder", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportSkippedCaseUiPlan(", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportLivePreview(", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportResultEntryFactory.CreateSkippedCase(", generatedCaseControllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_batch_import_case_progress_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseProgressUiController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Case-Progress-UI-Weiterleitung soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportCaseProgressUiController.Apply(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetProgressValue(caseIndex + 1);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseProgressPresentationBuilder.Build(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetStatusText(progressPresentation.StatusText);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in progressPresentation.LogLines)", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_case_loop_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportCaseLoopController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (var i = 0; i < casesToProcess.Count; i++)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex) when (ex is not OperationCanceledException)", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_case_persistenz_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var caseWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseWorkflowController.cs"));
        var persistenceWorkflowPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCasePersistenceWorkflowController.cs");
        var stateSaveControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseStateSaveController.cs");
        var persistenceUiControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportSamplePersistenceUiController.cs");
        var persistenceWorkflowSource = File.ReadAllText(persistenceWorkflowPath);
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.False(File.Exists(stateSaveControllerPath), "Triviale Best-Effort-State-Save-Logik soll im Persistence-Workflow leben.");
        Assert.False(File.Exists(persistenceUiControllerPath), "Triviale Persistence-UI-Weiterleitung soll im Persistence-Workflow leben.");
        Assert.Contains("processedCount % 5 == 0", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCaseStateSaveController", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportSamplePersistenceUiController.Apply(", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.Log(persistence.CandidateLogMessage);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.InvokeOnUi(() =>", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetSampleCount(persistence.SampleCount);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetCodesCovered(persistence.CodesCovered);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.Log(persistence.StoredLogMessage);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportSamplePersistenceUiController.Apply(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("void UpdateCounters()", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbSampleCount = persistence.SampleCount", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbCodesCovered = persistence.CodesCovered", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(persistence.CandidateLogMessage)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(persistence.StoredLogMessage)", viewModelSource, StringComparison.Ordinal);
    }

}
