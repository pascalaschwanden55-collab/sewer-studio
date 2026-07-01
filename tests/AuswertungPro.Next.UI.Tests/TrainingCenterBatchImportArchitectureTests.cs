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
        var cancelSource = ExtractMethodBody(source, "private void CancelBatch()");

        Assert.Contains("_genCts?.Cancel();", cancelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Abbruch angefordert...\";", cancelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportRunControlController.RequestCancel(", cancelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportTerminalPresentationBuilder.BuildCancelRequestedStatus", cancelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_run_preparation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportRunPreparationController.Prepare(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("_rootFolders.Count", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("_genCts = runPreparation.CancellationTokenSource;", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts?.Cancel();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts?.Dispose();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts = new CancellationTokenSource();", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.False(File.Exists(controllerPath), "Trivialer Batch-Startzustand soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingBatchImportRunStartController.Apply(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetBusy(true);", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetLogText(\"\");", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetProgressValue(0);", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("batchUi.SetProgressMax(1);", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("ClearLivePreview();", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("ResetSelfTrainingVisuals();", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportAutoApproveConfirmationController.Confirm(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("DialogHost.Current);", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var bestaetigung = DialogHost.Current.ConfirmWarn(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Trotzdem unge", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch-Import + KB (", batchImportSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_fehlerbehandlung_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportRunExceptionController.RecordCaseFailure(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunExceptionController.ApplyCanceled(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunExceptionController.ApplyFatal(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log($\"  FEHLER:", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(\"Batch-Import abgebrochen durch Benutzer.\")", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log($\"FATALER FEHLER:", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportRunCompletionController.CompleteAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Samples", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.BuildNoNewStatus(casesToProcess.Count)", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.BuildCompletionStatus()", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Samples.Clear", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Samples.Add", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(\"F", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("batchUi.SetBusy(false);", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportRunFinalizerController.Apply(", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportScanWorkflowController.RunAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Clear();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("STOP: Keine Ordner mit Protokoll-Dateien gefunden.", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportScanPresentationBuilder.BuildSummary(found.Count, casesWithProtocol.Count)", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportRuntimeSetupController.PrepareAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsAiSettingsProvider()", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var settings = await TrainingCenterSettingsStore.LoadAsync();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var allSamples = await TrainingSamplesStore.LoadAsync();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var existingSigs = allSamples.Select", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var casesToProcess = casesWithProtocol;", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var runSummary = new TrainingBatchImportRunSummary();", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseCandidateWorkflowController.Apply(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportGeneratedCaseUiController.Apply(", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var plan in generatedCasePlan.SampleUiPlans)", candidateWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSummary.AddNewSamples(generatedCasePlan.NewSampleCount)", candidateWorkflowSource, StringComparison.Ordinal);
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
    public void TrainingCenterViewModel_delegiert_batch_import_case_progress_ui_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportCaseProgressUiController.Apply(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProgressValue = i + 1;", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCaseProgressPresentationBuilder.Build(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusText = progressPresentation.StatusText", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var line in progressPresentation.LogLines)", batchImportSource, StringComparison.Ordinal);
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
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportCaseLoopController.RunAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (var i = 0; i < casesToProcess.Count; i++)", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex) when (ex is not OperationCanceledException)", batchImportSource, StringComparison.Ordinal);
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
        var persistenceWorkflowSource = File.ReadAllText(persistenceWorkflowPath);
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.False(File.Exists(stateSaveControllerPath), "Triviale Best-Effort-State-Save-Logik soll im Persistence-Workflow leben.");
        Assert.Contains("processedCount % 5 == 0", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCaseStateSaveController", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportSamplePersistenceUiController.Apply(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("void UpdateCounters()", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbSampleCount = persistence.SampleCount", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbCodesCovered = persistence.CodesCovered", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(persistence.CandidateLogMessage)", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log(persistence.StoredLogMessage)", batchImportSource, StringComparison.Ordinal);
    }

}
