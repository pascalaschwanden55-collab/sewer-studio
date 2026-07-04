using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterBatchImportArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_cancel_an_run_control_controller()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunControlController.cs");
        var cancelSource = ExtractMethodBody(source, "private void CancelBatch()");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("TrainingBatchImportRunControlController.Cancel(", cancelSource, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_genCts)", cancelSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            cancelSource,
            "CancellationTokenSourceLifecycle.CancelIfPresent(_genCts);",
            "StatusText = \"Abbruch angefordert...\";",
            "_genCts?.Cancel();",
            "_genCts?.Dispose();",
            "new CancellationTokenSource();",
            "TrainingBatchImportTerminalPresentationBuilder.BuildCancelRequestedStatus");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_run_preparation_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var workflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCommandWorkflow.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingBatchImportCommandWorkflow.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("if (request.GetIsBusy())", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.RootFolders.Count == 0", workflowSource, StringComparison.Ordinal);
        Assert.Contains("Bitte zuerst einen oder mehrere Ordner wählen.", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.CreateCancellationSource()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ConfirmAutoApprove()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.StoreCancellationSource(runCts)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.RunImportAsync(runCts.Token)", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            viewModelSource,
            "if (IsBusy) return;",
            "StatusText = \"Bitte zuerst einen oder mehrere Ordner wählen.\";",
            "var runCts = new CancellationTokenSource();",
            "runCts.Dispose();",
            "var ct = runCts.Token;");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_run_verdrahtung_an_command_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchMethod = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");
        var commandFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCommandRequestFactory.cs"));
        var runWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunWorkflow.cs"));
        var runRequestFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunRequestFactory.cs"));

        Assert.Contains("new TrainingBatchImportCommandRunDefaultRequestFactoryRequest(", batchMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportRunWorkflow.RunAsync(", batchMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportRunRequestFactory.CreateWithDefaults(", batchMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("new TrainingBatchImportRunDefaultRequestFactoryRequest(", batchMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("RunImportAsync: async ct =>", batchMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunWorkflow.RunAsync", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunRequestFactory.CreateWithDefaults(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportRunDefaultRequestFactoryRequest(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("RunWorkflowAsync: TrainingBatchImportWorkflow.RunAsync", runRequestFactorySource, StringComparison.Ordinal);
        Assert.Contains("request.RunWorkflowAsync(", runWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportWorkflowRequest(", runWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            source,
            "TrainingBatchImportWorkflow.RunAsync(",
            "new TrainingBatchImportWorkflowRequest(",
            "TrainingBatchImportCaseLoopController.RunAsync(",
            "TrainingBatchImportCaseWorkflowController.ProcessAsync(",
            "TrainingBatchImportRuntimeSetupController.PrepareAsync(",
            "TrainingBatchImportScanWorkflowController.RunAsync(",
            "TrainingBatchImportRunCompletionController.CompleteAsync(",
            "TrainingBatchImportCaseProgressPresentationBuilder.Build(");
    }

    [Fact]
    public void TrainingBatchImportWorkflow_setzt_batch_import_startzustand()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunStartController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(controllerPath), "Trivialer Batch-Startzustand soll inline in der VM stehen.");
        Assert.Contains("request.BatchUi.SetBusy(true);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetLogText(\"\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetProgressValue(0);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetProgressMax(1);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ClearLivePreview();", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ResetSelfTrainingVisuals();", workflowSource, StringComparison.Ordinal);
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
        var factorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCommandRequestFactory.cs"));
        var viewModelSource = source;

        Assert.DoesNotContain("TrainingBatchImportAutoApproveConfirmationController.Confirm(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DialogHost.Current", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportAutoApproveConfirmationController.Confirm(", factorySource, StringComparison.Ordinal);
        Assert.Contains("DialogHost.Current", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            viewModelSource,
            "var bestaetigung = DialogHost.Current.ConfirmWarn(",
            "Trotzdem unge",
            "Batch-Import + KB (");
    }

    [Fact]
    public void TrainingBatchImportWorkflow_setzt_batch_import_fehlerbehandlung()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunExceptionController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Batch-Import-Fehlerbehandlung soll inline in der VM stehen.");
        Assert.Contains("runtimeSetup.RunSummary.RecordError(ex.Message);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.Log($\"  FEHLER: {ex.Message}\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.Log(\"Batch-Import abgebrochen durch Benutzer.\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetStatusText(\"Batch-Import abgebrochen.\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.Log($\"FATALER FEHLER: {ex.Message}\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetStatusText($\"Fehler beim Batch-Import: {ex.Message}\");", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_abschluss_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var viewModelSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunRequestFactory.cs"));
        var workflowSource = source;

        Assert.Contains("TrainingBatchImportRunCompletionController.CompleteAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleCollectionController.ReplaceWith(request.Samples", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "runSummary.BuildNoNewStatus(casesToProcess.Count)",
            "runSummary.BuildCompletionStatus()",
            "Samples.Clear",
            "Samples.Add",
            "Log(\"F");
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_batch_import_final_state_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("request.BatchUi.SetBusy(false);", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "TrainingBatchImportRunFinalizerController.Apply(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_scan_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("TrainingBatchImportScanWorkflowController.RunAsync(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "Cases.Clear();",
            "STOP: Keine Ordner mit Protokoll-Dateien gefunden.",
            "TrainingBatchImportScanPresentationBuilder.BuildSummary(found.Count, casesWithProtocol.Count)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_runtime_setup_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("TrainingBatchImportRuntimeSetupController.PrepareAsync(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "var allSamples = await TrainingSamplesStore.LoadAsync();",
            "var existingSigs = allSamples.Select",
            "var casesToProcess = casesWithProtocol;",
            "var runSummary = new TrainingBatchImportRunSummary();");
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
        Assert.Contains("var allSamples = await loadSamplesAsync().ConfigureAwait(false);", controllerSource, StringComparison.Ordinal);
        Assert.Contains("var existingSigs = allSamples.Select(s => s.Signature)", controllerSource, StringComparison.Ordinal);
        Assert.Contains("ToHashSet(StringComparer.Ordinal)", controllerSource, StringComparison.Ordinal);
        Assert.Contains("log($\"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)\");", controllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_generated_case_ui_an_controller()
    {
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
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

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseCandidateWorkflowController.Apply(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportGeneratedCaseUiController.Apply(", candidateWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            candidateWorkflowSource,
            "generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped",
            "foreach (var plan in generatedCasePlan.SampleUiPlans)",
            "runSummary.AddNewSamples(generatedCasePlan.NewSampleCount)");
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
        AssertNoForbiddenTokens(
            caseWorkflowSource,
            "Action<TrainingBatchImportLivePreview> updateLivePreview",
            "Action<Action> invokeOnUi",
            "Action<SelfTrainingEntryResult> addResult",
            "Action<string, MatchLevel> updateCodeDistribution");
        AssertNoForbiddenTokens(
            persistenceWorkflowSource,
            "Action<Action> invokeOnUi",
            "Action<int> setSampleCount",
            "Action<int> setCodesCovered");
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
        Assert.Contains("new TrainingBatchImportSkippedCaseUiPlan(", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportLivePreview(", generatedCaseControllerSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportResultEntryFactory.CreateSkippedCase(", generatedCaseControllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingBatchImportWorkflow_setzt_trivialen_batch_import_case_progress_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCaseProgressUiController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Case-Progress-UI-Weiterleitung soll inline im Workflow stehen.");
        Assert.Contains("request.BatchUi.SetProgressValue(caseIndex + 1);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCaseProgressPresentationBuilder.Build(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.BatchUi.SetStatusText(progressPresentation.StatusText);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in progressPresentation.LogLines)", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_case_loop_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("TrainingBatchImportCaseLoopController.RunAsync(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "for (var i = 0; i < casesToProcess.Count; i++)",
            "catch (Exception ex) when (ex is not OperationCanceledException)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_case_persistenz_an_controller()
    {
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));
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

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(", caseWorkflowSource, StringComparison.Ordinal);
        Assert.False(File.Exists(stateSaveControllerPath), "Triviale Best-Effort-State-Save-Logik soll im Persistence-Workflow leben.");
        Assert.False(File.Exists(persistenceUiControllerPath), "Triviale Persistence-UI-Weiterleitung soll im Persistence-Workflow leben.");
        Assert.Contains("processedCount % 5 == 0", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.Log(persistence.CandidateLogMessage);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.InvokeOnUi(() =>", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetSampleCount(persistence.SampleCount);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.SetCodesCovered(persistence.CodesCovered);", persistenceWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("caseUi.Log(persistence.StoredLogMessage);", persistenceWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(",
            "TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(",
            "void UpdateCounters()",
            "KbSampleCount = persistence.SampleCount",
            "KbCodesCovered = persistence.CodesCovered",
            "Log(persistence.CandidateLogMessage)",
            "Log(persistence.StoredLogMessage)");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte Batch-Import-Logik gefunden: " + string.Join(", ", hits));
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Methode nicht gefunden: {signature}");

        var openBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openBraceIndex > signatureIndex, $"Methoden-Anfang nicht gefunden: {signature}");

        var depth = 0;
        for (var i = openBraceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
                depth--;

            if (depth == 0)
                return source[signatureIndex..(i + 1)];
        }

        throw new InvalidOperationException($"Methoden-Ende nicht gefunden: {signature}");
    }
}
