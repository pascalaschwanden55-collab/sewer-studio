using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSelfTrainingArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_run_command_an_workflow()
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
            "SelfTrainingRunPreparationWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunPreparationRequestFactory.cs"));
        var commandWorkflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunCommandWorkflow.cs");
        var commandFactoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunCommandRequestFactory.cs");
        var runMethodSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.True(File.Exists(commandWorkflowPath), commandWorkflowPath);
        Assert.True(File.Exists(commandFactoryPath), commandFactoryPath);
        var commandWorkflowSource = File.ReadAllText(commandWorkflowPath);
        var commandFactorySource = File.ReadAllText(commandFactoryPath);

        Assert.Contains("SelfTrainingRunCommandWorkflow.RunAsync(", runMethodSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunCommandRequestFactory.CreateWithDefaults(", runMethodSource, StringComparison.Ordinal);
        Assert.Contains("new SelfTrainingRunCommandDefaultRequestFactoryRequest(", runMethodSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPreparationWorkflow.RunAsync(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPreparationRequestFactory.CreateWithDefaults(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("new SelfTrainingRunPreparationDefaultRequestFactoryRequest(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("var preparation = await request.PrepareAsync()", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("preparation.ShouldStop", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("request.CreateRunRequest(", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("Directory.Exists", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseInputMapper.ToTrainingCase", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", factorySource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingAutoScanController.RunAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingCaseSelectionController.Select(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ResetCancellation()", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            runMethodSource,
            "SelfTrainingRunPreparationWorkflow.RunAsync(",
            "SelfTrainingRunPreparationRequestFactory.CreateWithDefaults(",
            "new SelfTrainingRunPreparationDefaultRequestFactoryRequest(",
            "new SelfTrainingRunPreparationWorkflowRequest(",
            "TrainingCenterRuntimeHelpers.ToTrainingCase",
            "Select(TrainingCenterRuntimeHelpers.ToTrainingCase)",
            "Directory.Exists",
            "TrainingSamplesStore.LoadAsync",
            "SelfTrainingAutoScanController.RunAsync(",
            "SelfTrainingCaseSelectionController.Select(",
            "existingSamplesForSelection =",
            "_selfTrainingCts?.Cancel();",
            "_selfTrainingCts?.Dispose();",
            "_selfTrainingCts = new CancellationTokenSource();",
            "var ct = _selfTrainingCts.Token;");
    }

    [Fact]
    public void TrainingCenterRuntimeHelpers_ist_aus_viewmodel_ordner_entfernt()
    {
        var path = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterRuntimeHelpers.cs");

        Assert.False(File.Exists(path), path);
    }

    [Fact]
    public void Training_meter_timeline_erzeugung_liegt_in_einer_training_factory()
    {
        var repoRoot = FindRepoRoot();
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingMeterTimelineServiceFactory.cs");
        var sampleRuntimeSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterSampleGenerationRuntime.cs"));
        var batchImportSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportWorkflow.cs"));

        Assert.True(File.Exists(factoryPath), factoryPath);
        var factorySource = File.ReadAllText(factoryPath);
        Assert.Contains("TrainingMeterTimelineServiceFactory.Create", sampleRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("TrainingMeterTimelineServiceFactory.Create", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("new OllamaClient(", factorySource, StringComparison.Ordinal);
        Assert.Contains("new OllamaVisionFindingsService(", factorySource, StringComparison.Ordinal);
        Assert.Contains("new OsdMeterDetectionService(", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            sampleRuntimeSource,
            "private static MeterTimelineService CreateMeterTimelineService",
            "new OllamaClient(",
            "new OllamaVisionFindingsService(",
            "new OsdMeterDetectionService(");
        AssertNoForbiddenTokens(
            batchImportSource,
            "private static MeterTimelineService CreateMeterTimelineService",
            "new OllamaClient(",
            "new OllamaVisionFindingsService(",
            "new OsdMeterDetectionService(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_cancellation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var stopSource = ExtractMethodBody(source, "private void StopSelfTraining()");

        Assert.Contains("private readonly SelfTrainingCancellationController _selfTrainingCancellation = new();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCancellation.Reset", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunControlController.Stop(", stopSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCancellation.Cancel", stopSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(viewModelSource, "_selfTrainingCts");
    }

    [Fact]
    public void SelfTrainingRunPreparationWorkflow_delegiert_self_training_auto_scan_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunPreparationWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("SelfTrainingAutoScanController.RunAsync(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "SelfTrainingAutoScanController.ShouldScan(",
            "SelfTrainingAutoScanController.ScanAsync(",
            "SelfTrainingAutoScanController.StatusText",
            "if (Cases.Count == 0 && _rootFolders.Count > 0)",
            "foreach (var c in autoScannedCases)",
            "Cases.Add(c);");
    }

    [Fact]
    public void SelfTrainingRunPreparationWorkflow_setzt_self_training_case_selection_orchestrierung()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunPreparationWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("if (request.SelectedCase is null)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("existingSamplesForSelection = await request.LoadSamplesAsync()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingCaseSelectionController.Select(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.SetSelectedCase(selectedCase);", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "SelfTrainingCaseSelectionController.WithProtocolOrStop");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_run_request_erzeugung_an_command_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunRequestFactory.cs");
        var commandFactoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunCommandRequestFactory.cs");
        var runMethodSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.True(File.Exists(commandFactoryPath), commandFactoryPath);
        var commandFactorySource = File.ReadAllText(commandFactoryPath);

        Assert.Contains("SelfTrainingRunCommandWorkflow.RunAsync(", runMethodSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunCommandRequestFactory.CreateWithDefaults(", runMethodSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunWorkflow.RunAsync", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunRequestFactory.CreateWithDefaults(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("new SelfTrainingRunRequestFactoryRequest(", commandFactorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            runMethodSource,
            "SelfTrainingRunWorkflow.RunAsync(",
            "SelfTrainingRunRequestFactory.CreateWithDefaults(",
            "new SelfTrainingRunRequestFactoryRequest(",
            "new SelfTrainingUiSink(",
            "new SelfTrainingRunWorkflowRequest(",
            "PrepareRuntimeAsync: log =>",
            "LoadRetrievalConfig:",
            "GetOrCreateKbHttpClient:",
            "CreateSession:",
            "PlayerAiSettingsLoader.LoadPlatformSettings().ToOllamaConfig()",
            "new System.Net.Http.HttpClient",
            "SelfTrainingSessionController.Create(",
            "SelfTrainingRuntimeSetupController.PrepareAsync(",
            "SelfTrainingHistorySnapshotBuilder.Build(result, DateTime.UtcNow)",
            "SelfTrainingRunPresentationBuilder.BuildCompletion(result)",
            "SelfTrainingReviewCandidateSelector.HasReviewableMatches(result)",
            "SelfTrainingReviewQueueController.EnqueueCandidates(",
            "Log(\"Selbsttraining abgebrochen.\");",
            "Log($\"FEHLER: {ex.GetType().Name}: {ex.Message}\");");
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_triviale_self_training_review_queue_orchestrierung()
    {
        var repoRoot = FindRepoRoot();
        var workflowControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingReviewQueueWorkflowController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var workflowSource = source;

        Assert.False(File.Exists(workflowControllerPath), workflowControllerPath);
        Assert.Contains("request.ReviewQueueService is not null", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingReviewCandidateSelector.HasReviewableMatches(result)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("var reviewSamples = await request.LoadSamplesAsync()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingReviewQueueController.EnqueueCandidates(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ReloadReviewQueue(request.ReviewQueueService);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.Log(reviewQueueUpdate.LogMessage ?? \"\");", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "ReviewQueueServiceRef.EnqueueFromSelfTraining(",
            "SelfTrainingReviewCandidateSelector.SelectForRun(allSamplesForReview, result)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_kb_update_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));

        Assert.Contains("request.UpdateKbAsync(result, request.CancellationToken)", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            workflowSource,
            "SelfTrainingKbUpdateController.ShouldRun(result)",
            "SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(",
            "SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)",
            "SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)",
            "s.KbIndexState is KbIndexState.None or KbIndexState.Error",
            "stOutcome.IndexedIds.ToHashSet()");
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_trivialen_self_training_startzustand()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var startControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunStartController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(startControllerPath), "Trivialer Self-Training-Startzustand soll inline im Workflow stehen.");
        Assert.Contains("request.Ui.SetBusy(true);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetSelfTrainingRunning(true);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ResetVisuals();", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetLogText(\"\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildStart(request.SelectedCase)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetStatusText(startPresentation.StatusText);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in startPresentation.LogLines)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog()", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_ollama_log_an_presenter()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var setupSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRuntimeSetupController.cs"));
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));

        Assert.Contains("request.PrepareRuntimeAsync(request.Ui.Log)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(", setupSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}");
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_triviale_self_training_completion()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var completionControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunCompletionController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(completionControllerPath), "Triviale Self-Training-Completion-Sequenz soll inline im Workflow stehen.");
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildCompletion(result)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in completionPresentation.LogLines)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetStatusText(completionPresentation.StatusText);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(result)", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_runtime_setup_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var setupSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRuntimeSetupController.cs"));
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));

        Assert.Contains("request.PrepareRuntimeAsync(request.Ui.Log)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("using var selfTrainingSetup", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingSessionController.Create(", setupSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "SelfTrainingSessionController.Create(");
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_triviale_self_training_run_execution()
    {
        var repoRoot = FindRepoRoot();
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunExecutionController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var workflowSource = source;

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.Contains("selfTrainingSetup.Session.Orchestrator.RunAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistorySnapshotBuilder.Build(result, request.UtcNow())", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.AppendHistoryAsync(snapshot)", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_trivialen_self_training_post_run_refresh()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("await request.LoadSamplesInternalAsync()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.RefreshKbStatusAsync()", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "SelfTrainingPostRunRefreshController.RefreshAsync(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_last_match_rate_refresh_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var loadSource = ExtractMethodBody(source, "private async Task LoadLastMatchRateAsync()");
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingLastMatchRateRefreshWorkflow.cs");
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingLastMatchRateRefreshRequestFactory.cs");

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.True(File.Exists(factoryPath), factoryPath);
        var workflowSource = File.ReadAllText(workflowPath);
        var factorySource = File.ReadAllText(factoryPath);

        Assert.Contains("SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(", loadSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLastMatchRateRefreshRequestFactory.CreateWithDefaults(", loadSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistoryStore.LoadAsync", factorySource, StringComparison.Ordinal);
        Assert.Contains("new SelfTrainingLastMatchRateRefreshWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLastMatchRatePresentationBuilder.Build(runs)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingMatchRatePresentationController.Apply(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            loadSource,
            "SelfTrainingHistoryStore.LoadAsync",
            "SelfTrainingLastMatchRateRefreshRequestFactory.Create(",
            "new SelfTrainingLastMatchRateRefreshWorkflowRequest(",
            "SelfTrainingLastMatchRatePresentationBuilder.Build(",
            "SelfTrainingMatchRatePresentationController.Apply(",
            "catch {");
        AssertNoForbiddenTokens(
            viewModelSource,
            "runs[^1]",
            "ExactPercent = last.ExactPercent",
            "PartialPercent = last.PartialPercent",
            "MismatchPercent = last.MismatchPercent",
            "NoFindingsPercent = last.NoFindingsPercent");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_load_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = ExtractMethodBody(source, "public async Task LoadAsync()");
        var workflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterLoadWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterLoadRequestFactory.cs"));

        Assert.Contains("TrainingCenterLoadWorkflow.RunAsync(", loadSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterLoadRequestFactory.CreateWithDefaults(", loadSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterLoadDefaultRequestFactoryRequest(", loadSource, StringComparison.Ordinal);
        Assert.Contains("Directory.Exists", factorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterLoadWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.RestoreExistingRootFolders(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.ReplaceRootFolders(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.LoadSamplesAsync()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.RefreshKbStatusAsync()", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.LoadLastMatchRateAsync()", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            loadSource,
            "_store.LoadAsync()",
            "Directory.Exists",
            "new TrainingCenterLoadWorkflowRequest(",
            "TrainingCenterStateController.RestoreExistingRootFolders(",
            "TrainingCenterStateController.ReplaceRootFolders(",
            "StatusText = $\"Geladen:",
            "await LoadSamplesInternalAsync();",
            "await RefreshKbStatusAsync();",
            "await LoadLastMatchRateAsync();");
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_triviale_self_training_exceptions()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var controllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunExceptionController.cs");
        var workflowSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Self-Training-Exception-UI soll inline im Workflow stehen.");
        Assert.Contains("request.Ui.Log(\"Selbsttraining abgebrochen.\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetStatusText(\"Selbsttraining abgebrochen.\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.Log($\"FEHLER: {ex.GetType().Name}: {ex.Message}\");", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetStatusText($\"Fehler: {ex.Message}\");", workflowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfTrainingRunWorkflow_setzt_trivialen_self_training_final_state()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunWorkflow.cs"));
        var workflowSource = source;

        Assert.Contains("request.Ui.SetBusy(false);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Ui.SetSelfTrainingRunning(false);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.SetOrchestrator(null);", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, "SelfTrainingRunFinalizerController.Apply(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_stop_und_pause_control_an_controller()
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
            "SelfTrainingRunControlController.cs");
        var stopSource = ExtractMethodBody(source, "private void StopSelfTraining()");
        var pauseSource = ExtractMethodBody(source, "private void PauseSelfTraining()");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("SelfTrainingRunControlController.Stop(", stopSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunControlController.TogglePause(", pauseSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(source, "_selfTrainingCts");
        AssertNoForbiddenTokens(
            stopSource,
            "_selfTrainingCancellation.Cancel();",
            "StatusText = \"Selbsttraining wird abgebrochen...\";");
        AssertNoForbiddenTokens(
            pauseSource,
            "if (_selfTrainingOrchestrator is null) return;",
            "if (_selfTrainingOrchestrator.IsPaused)",
            "_selfTrainingOrchestrator.Resume();",
            "StatusText = \"Selbsttraining fortgesetzt.\";",
            "Log(\"Pipeline fortgesetzt.\");",
            "_selfTrainingOrchestrator.Pause();",
            "StatusText = \"Selbsttraining pausiert.\";",
            "Log(\"Pipeline pausiert.\");");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_index_loop_an_runner()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var methodSource = ExtractMethodBody(source, "private async Task<KbIndexOutcome> IncrementalKbUpdateWithReasonAsync(");
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseIndexWorkflow.cs"));

        Assert.Contains("TrainingKnowledgeBaseIndexWorkflow.RunWithDefaultsAsync(", methodSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKbIndexRunner.CreateDefault(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("return runner.RunAsync;", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "TrainingKbIndexRunner.CreateDefault(",
            "new AppSettingsAiSettingsProvider()",
            "new System.Net.Http.HttpClient",
            "foreach (var sample in samples)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_sample_id_aufloesung_an_workflow()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var methodSource = ExtractMethodBody(source, "private async Task<string?> ResolveSelfTrainingSampleIdAsync(");
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewSampleIdResolutionWorkflow.cs"));

        Assert.Contains("TrainingReviewSampleIdResolutionWorkflow.ResolveWithDefaultsAsync(", methodSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingReviewSampleIdResolver.ResolveAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "SelfTrainingReviewSampleIdResolver.ResolveAsync(",
            "TrainingSamplesStore.LoadAsync",
            "Math.Abs");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_gold_kb_reconcile_run_an_command_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var runWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingGoldKbReconcileRunWorkflow.cs"));
        var commandWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingGoldKbReconcileCommandWorkflow.cs"));
        var commandFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingGoldKbReconcileCommandRequestFactory.cs"));
        var methodSource = ExtractMethodBody(viewModelSource, "private async Task ReconcileGoldToKbAsync()");

        Assert.Contains("TrainingGoldKbReconcileCommandWorkflow.RunAsync(", methodSource, StringComparison.Ordinal);
        Assert.Contains("TrainingGoldKbReconcileCommandRequestFactory.CreateWithDefaults(", methodSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingGoldKbReconcileCommandDefaultRequestFactoryRequest(", methodSource, StringComparison.Ordinal);
        Assert.Contains("TrainingGoldKbReconcileRequestFactory.CreateWithDefaults(", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingGoldKbReconcileRunWorkflow.RunAsync", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingGoldKbReconcileWorkflowController.RunAsync(", runWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.MergeOrUpdateAsync", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("IncrementalKbUpdateWithReasonAsync", viewModelSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "TrainingGoldKbReconcileCommandRequestFactory.Create(",
            "new TrainingGoldKbReconcileCommandRequestFactoryRequest(",
            "RunReconcileAsync:",
            "TrainingGoldKbReconcileRunWorkflow.RunAsync",
            "if (IsBusy || IsSelfTrainingRunning) return;",
            "var ct = ResetGenerationCancellation();",
            "TrainingGoldKbReconcileRequestFactory.CreateWithDefaults(",
            "TrainingSamplesStore.LoadAsync",
            "TrainingSamplesStore.MergeOrUpdateAsync",
            "KnowledgeBackupService.ExportAsync",
            "KnowledgeBasePaths.GetRoot",
            "DateTime.Now",
            "System.IO.Directory.CreateDirectory",
            "TrainingGoldKbReconcileWorkflowController.RunAsync(",
            "Log(\"KB-Nachholen abgebrochen.\");",
            "Log($\"KB-Nachholen Fehler: {ex.Message}\");",
            "KbReconcilePlanner.SelectPending",
            "const int batchSize",
            "foreach (var s in batch)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_queue_abschluss_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewItemDecisionWorkflow.cs"));

        Assert.Contains("TrainingReviewQueueCompletionController.ApplyApproved(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewQueueCompletionController.ApplyRejected(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            source,
            "queueService.Remove(item.Id);",
            "ReviewQueue.Remove(item);");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_item_entscheidung_an_workflow()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var commandWorkflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewItemDecisionCommandWorkflow.cs"));
        var commandFactorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewItemDecisionCommandRequestFactory.cs"));
        var approveMethod = ExtractMethodBody(source, "public async Task ApproveReviewItemAsync(");
        var rejectMethod = ExtractMethodBody(source, "public async Task RejectReviewItemAsync(");
        var commandMethod = ExtractMethodBody(source, "private Task RunReviewItemDecisionAsync(");

        Assert.Contains("RunReviewItemDecisionAsync(", approveMethod, StringComparison.Ordinal);
        Assert.Contains("RunReviewItemDecisionAsync(", rejectMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewItemDecisionCommandWorkflow.RunAsync(", commandMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewItemDecisionCommandRequestFactory.Create(", commandMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingReviewItemDecisionCommandWorkflowRequest(", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("RunDecisionAsync: TrainingReviewItemDecisionWorkflow.RunAsync", commandFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewItemDecisionRequestFactory.CreateWithCurrentUser(", commandWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("request.RunDecisionAsync(", commandWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            approveMethod + rejectMethod + commandMethod,
            "new TrainingReviewItemDecisionCommandWorkflowRequest(",
            "TrainingReviewItemDecisionRequestFactory.CreateWithCurrentUser(",
            "CreateReviewItemDecisionRequest(",
            "new TrainingReviewItemDecisionWorkflowRequest",
            "System.Environment.UserName",
            "item.Entry is not null",
            "item.IsFromSelfTraining",
            "ApproveSelfTrainingAsync(sampleId",
            "RejectSelfTrainingAsync(",
            "TrainingReviewQueueCompletionController.ApplyApproved",
            "TrainingReviewQueueCompletionController.ApplyRejected",
            "LoadSamplesInternalAsync().ConfigureAwait(false)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_approval_service_erzeugung_an_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewApprovalServiceFactory.cs"));
        var commandMethod = ExtractMethodBody(source, "private Task RunReviewItemDecisionAsync(");
        var commandWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewItemDecisionCommandWorkflow.cs"));

        Assert.Contains("TrainingReviewApprovalServiceFactory.Create(", commandWorkflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingReviewApprovalServiceFactory.Create(", commandMethod, StringComparison.Ordinal);
        Assert.Contains("new DelegatingKnowledgeBaseIndexer(", factorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingSamplesStoreAdapter()", factorySource, StringComparison.Ordinal);
        Assert.Contains("new ReviewApprovalService(", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            source,
            "private IReviewApprovalService BuildReviewApprovalService()",
            "new DelegatingKnowledgeBaseIndexer(",
            "new TrainingSamplesStoreAdapter()",
            "new ReviewApprovalService(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_selected_review_commands_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var approveMethod = ExtractMethodBody(source, "private async Task ApproveSelectedReviewAsync(");
        var rejectMethod = ExtractMethodBody(source, "private async Task RejectSelectedReviewAsync(");
        var correctionMethod = ExtractMethodBody(source, "public async Task ApplyReviewCorrectionAsync(");
        var startdataMethod = ExtractMethodBody(source, "public async Task ApproveAllStartdataAsync(");
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSelectedReviewCommandRequestFactory.cs");
        var startdataFactoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataApprovalRequestFactory.cs");

        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.True(File.Exists(startdataFactoryPath), startdataFactoryPath);
        var factorySource = File.ReadAllText(factoryPath);
        var startdataFactorySource = File.ReadAllText(startdataFactoryPath);
        Assert.Contains("TrainingSelectedReviewCommandWorkflow.ApproveAsync(", approveMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewCommandWorkflow.RejectAsync(", rejectMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewCommandWorkflow.CorrectAsync(", correctionMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewCommandRequestFactory.CreateApproveWithDefaults(", approveMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewCommandRequestFactory.CreateRejectWithDefaults(", rejectMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewCommandRequestFactory.CreateCorrectionWithDefaults(", correctionMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewRuntime.RejectWithDefaultsAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewRuntime.CorrectWithDefaultsAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(", startdataFactorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            approveMethod + rejectMethod + correctionMethod + startdataMethod,
            "TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(",
            "TrainingSelectedReviewRuntime.RejectWithDefaultsAsync(",
            "TrainingSelectedReviewRuntime.CorrectWithDefaultsAsync(");
        AssertNoForbiddenTokens(
            approveMethod + rejectMethod + correctionMethod + startdataMethod,
            "        try",
            "catch (Exception ex)",
            "Review-Freigabe Fehler",
            "Review-Ablehnung Fehler",
            "Review-Korrektur Fehler",
            "OnUi(() => ReviewStatusText = $\"Fehler: {ex.Message}\")",
            "new KnowledgeBaseContext()",
            "CreateFeedbackService");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_pending_review_geometry_reset_an_controller()
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
            "TrainingReviewPendingGeometryController.cs");
        var selectedChangedSource = ExtractMethodBody(source, "partial void OnSelectedReviewItemChanged(");
        var clearSource = ExtractMethodBody(source, "private void ClearPendingReviewGeometry()");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("TrainingReviewPendingGeometryController.Clear(", selectedChangedSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewPendingGeometryController.Clear(", clearSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            selectedChangedSource + clearSource,
            "PendingBox = null;",
            "PendingSamMask = null;");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_feedback_service_erzeugung_an_factory()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSelectedReviewRuntime.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewFeedbackServiceFactory.cs"));

        Assert.Contains("TrainingReviewFeedbackServiceFactory.Create(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("new KnowledgeBaseContext()", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("new AuswertungPro.Next.Infrastructure.Ai.QualityGate.ValidationLogger", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            source,
            "TrainingReviewFeedbackServiceFactory.Create(",
            "new KnowledgeBaseContext()",
            "new AuswertungPro.Next.Infrastructure.Ai.QualityGate.ValidationLogger",
            "new AuswertungPro.Next.Infrastructure.Ai.QualityGate.WeightLearningService",
            "new KnowledgeBaseManager(db, embedder");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_queue_load_an_workflow()
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
            "TrainingReviewQueueLoadWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewQueueLoadRequestFactory.cs"));
        var viewModelSource = source;
        var loadMethod = ExtractMethodBody(source, "public void LoadReviewQueue(");

        Assert.Contains("TrainingReviewQueueLoadWorkflow.Run(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewQueueLoadRequestFactory.Create(", loadMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingReviewQueueLoadWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("var items = request.QueueService.GetAll();", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ReviewQueue.Clear();", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ReviewQueue.Add(item);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.SetReviewQueueCount(items.Count);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.SetReviewStatusText(BuildStatusText(items.Count));", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            loadMethod,
            "new TrainingReviewQueueLoadWorkflowRequest(",
            "var items = queueService.GetAll();",
            "ReviewQueue.Clear();",
            "foreach (var item in items)",
            "ReviewQueue.Add(item);",
            "ReviewQueueCount = items.Count;",
            "ReviewStatusText = $");
    }


    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_check_an_workflow()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var checkSource = ExtractMethodBody(source, "private async Task CheckKnowledgeBaseAsync()");
        var workflowSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseCheckWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseCheckRequestFactory.cs"));

        Assert.Contains("TrainingKnowledgeBaseCheckWorkflow.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRequestFactory.Create(", checkSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingKnowledgeBaseCheckWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.TryStart(request.IsBusy)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplySuccess(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplyFailure(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            checkSource,
            "new TrainingKnowledgeBaseCheckWorkflowRequest(");
        AssertNoForbiddenTokens(
            viewModelSource,
            "TrainingKnowledgeBaseCheckRunController.TryStart(",
            "TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary)",
            "TrainingKnowledgeBaseCheckRunController.ApplySuccess(",
            "TrainingKnowledgeBaseCheckRunController.ApplyFailure(",
            "summary.LatestVersionAtUtc.Value.ToLocalTime()",
            "summary.TopCodes.Count > 0",
            "KB-Stand: Samples=");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_status_und_quality_presentation_an_builder()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var refreshStatusSource = ExtractMethodBody(source, "private async Task RefreshKbStatusAsync()");
        var refreshQualitySource = ExtractMethodBody(source, "private async Task RefreshKbQualityAsync()");
        var statusWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseStatusRefreshWorkflow.cs"));
        var statusFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseStatusRefreshRequestFactory.cs"));
        var qualityWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseQualityRefreshWorkflow.cs"));
        var qualityFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseQualityRefreshRequestFactory.cs"));
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBasePresentationController.cs");
        var statusApplySource = ExtractMethodBody(source, "private void ApplyKbStatusPresentation(");
        var qualityApplySource = ExtractMethodBody(source, "private void ApplyKbQualityPresentation(");

        Assert.Contains("TrainingKnowledgeBaseStatusRefreshWorkflow.RunAsync(", refreshStatusSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseStatusRefreshRequestFactory.Create(", refreshStatusSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingKnowledgeBaseStatusRefreshWorkflowRequest(", statusFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseQualityRefreshWorkflow.RunAsync(", refreshQualitySource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseQualityRefreshRequestFactory.CreateWithDefaults(", refreshQualitySource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistoryStore.LoadAsync", qualityFactorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingKnowledgeBaseQualityRefreshWorkflowRequest(", qualityFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseStatusPresentationBuilder.Build(status)", statusWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs)", qualityWorkflowSource, StringComparison.Ordinal);
        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("TrainingKnowledgeBasePresentationController.ApplyStatus(", statusApplySource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBasePresentationController.ApplyQuality(", qualityApplySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            viewModelSource,
            "TrainingKnowledgeBaseStatusPresentationBuilder.Build(status)",
            "TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs)",
            "status.SampleCount switch",
            "TakeLast(5)",
            "quality.StaleSampleCount > 0");
        AssertNoForbiddenTokens(
            refreshStatusSource,
            "new TrainingKnowledgeBaseStatusRefreshWorkflowRequest(");
        AssertNoForbiddenTokens(
            refreshQualitySource,
            "SelfTrainingHistoryStore.LoadAsync",
            "TrainingKnowledgeBaseQualityRefreshRequestFactory.Create(",
            "new TrainingKnowledgeBaseQualityRefreshWorkflowRequest(");
        AssertNoForbiddenTokens(
            statusApplySource,
            "KbSampleCount = presentation.SampleCount",
            "KbErrorCount = presentation.ErrorCount",
            "KbNewCount = presentation.NewCount",
            "KbEmbeddingCount = presentation.EmbeddingCount",
            "KbCodesCovered = presentation.CodesCovered",
            "KbLastUpdate = presentation.LastUpdateText",
            "KbReadinessLabel = presentation.ReadinessLabel",
            "KbReadinessBrush = presentation.ReadinessBrush",
            "KbTopCodesText = presentation.TopCodesText");
        AssertNoForbiddenTokens(
            qualityApplySource,
            "KbCoverageGapsText = presentation.CoverageGapsText",
            "KbCoverageGapsCount = presentation.CoverageGapsCount",
            "KbAccuracyText = presentation.AccuracyText",
            "KbStaleSampleCount = presentation.StaleSampleCount",
            "KbTrendText = presentation.TrendText",
            "KbTrendDirection = presentation.TrendDirection");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sample_persistenz_request_an_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var persistMethod = ExtractMethodBody(source, "private async Task PersistSamplesAsync(");
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSamplePersistenceRequestFactory.cs");

        Assert.True(File.Exists(factoryPath), factoryPath);
        var factorySource = File.ReadAllText(factoryPath);

        Assert.Contains("TrainingSamplePersistenceWorkflowController.PersistAsync(", persistMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplePersistenceRequestFactory.CreateWithDefaults(", persistMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.MergeOrUpdateAsync", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            persistMethod,
            "Samples.ToList()",
            "TrainingSamplesStore.MergeOrUpdateAsync",
            "samples.ToList()");
        AssertNoForbiddenTokens(
            viewModelSource,
            "changedSample.KbIndexState = KbIndexState.Pending",
            "outcome.IndexedIds.Contains",
            "TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample>");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_approved_protocol_export_an_workflow()
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
            "TrainingApprovedProtocolExportWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingApprovedProtocolExportRequestFactory.cs"));
        var viewModelSource = source;
        var methodSource = ExtractMethodBody(source, "private async Task ExportApprovedAsync()");

        Assert.Contains("TrainingApprovedProtocolExportWorkflow.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingApprovedProtocolExportRequestFactory.CreateWithDefaults(", methodSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingApprovedProtocolExportDefaultRequestFactoryRequest(", methodSource, StringComparison.Ordinal);
        Assert.Contains("TrainingApprovedProtocolExportController.RunAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("ProtocolTrainingStore.AddSample", factorySource, StringComparison.Ordinal);
        Assert.Contains("ProtocolTrainingStore.DefaultPath", factorySource, StringComparison.Ordinal);
        Assert.Contains("UtcNow: () => DateTime.UtcNow", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "new TrainingApprovedProtocolExportWorkflowRequest(",
            "new TrainingApprovedProtocolExportRequestFactoryRequest(",
            "DateTime.UtcNow",
            "UtcNow:",
            "ProtocolTrainingStore.AddSample",
            "ProtocolTrainingStore.DefaultPath",
            "TargetPath: ProtocolTrainingStore.DefaultPath");
        AssertNoForbiddenTokens(
            viewModelSource,
            "TrainingApprovedProtocolExportController.RunAsync(",
            "Path.Combine(AppSettings.AppDataDir, \"data\", \"protocol_training.json\")",
            "foreach (var line in result.LogLines)",
            "new AuswertungPro.Next.Domain.Protocol.ProtocolEntry",
            "approved.Select(s => s.Code)",
            "s.ExportedUtc = DateTime.UtcNow");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sample_commands_an_workflow()
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
            "TrainingSampleCommandWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSampleCommandRequestFactory.cs"));
        var viewModelSource = source;
        var methodSource = ExtractMethodBody(source, "private async Task RunSampleCommandAsync(");

        Assert.Contains("TrainingSampleCommandWorkflow.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleCommandRequestFactory.CreateWithDefaults(", methodSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingSampleCommandRequestFactoryRequest(", methodSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Approve", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Reject", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Remove", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var decision = request.Decide(sample);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.DeindexSample(sample.SampleId);", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.PersistSamplesAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseSampleDeindexer.TryDeindexWithDefaults(", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "new TrainingSampleCommandWorkflowRequest(",
            "TryDeindexSample,",
            "value => StatusText = value",
            "PersistSamplesAsync));");
        AssertNoForbiddenTokens(
            viewModelSource,
            "var decision = TrainingSampleDecisionController.Approve(sample)",
            "var decision = TrainingSampleDecisionController.Reject(sample)",
            "var decision = TrainingSampleDecisionController.Remove(sample)",
            "if (decision.ShouldDeindex)",
            "TryDeindexSample(sample.SampleId)",
            "PersistSamplesAsync(decision.PersistChangedSample ? sample : null)",
            "SelectedSample.Status = TrainingSampleStatus.Approved",
            "SelectedSample.Status = TrainingSampleStatus.Rejected",
            "SelectedSample.Status = TrainingSampleStatus.Removed",
            "SelectedSample.KbIndexState = KbIndexState.None");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_sample_deindex_an_deindexer()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var deindexerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKnowledgeBaseSampleDeindexer.cs"));
        var methodSource = ExtractMethodBody(source, "private void TryDeindexSample(string sampleId)");

        Assert.Contains("TrainingKnowledgeBaseSampleDeindexer.TryDeindexWithDefaults(", methodSource, StringComparison.Ordinal);
        Assert.Contains("new KnowledgeBaseContext()", deindexerSource, StringComparison.Ordinal);
        Assert.Contains("new EmbeddingService", deindexerSource, StringComparison.Ordinal);
        Assert.Contains("new KnowledgeBaseManager", deindexerSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            methodSource,
            "new AppSettingsAiSettingsProvider()",
            "new System.Net.Http.HttpClient",
            "new KnowledgeBaseContext()",
            "new EmbeddingService",
            "new KnowledgeBaseManager",
            "kbManager.DeindexSample");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sample_generierung_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterSampleGenerationRequestFactory.cs"));
        var generateMethod = ExtractMethodBody(source, "private async Task GenerateSamplesAsync()");

        Assert.Contains("TrainingCenterSampleGenerationWorkflow.RunAsync(", generateMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterSampleGenerationRequestFactory.CreateWithDefaults(", generateMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterSampleGenerationDefaultRequestFactoryRequest(", generateMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterSampleGenerationRuntime.GenerateWithDiagnosticsAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.MergeAndSaveAsync", factorySource, StringComparison.Ordinal);
        Assert.Contains("AiTrack.Begin(\"Training Center\")", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            generateMethod,
            "new TrainingCenterSampleGenerationWorkflowRequest(",
            "AiTrack.Begin",
            "BeginActivity:",
            "new AppSettingsAiSettingsProvider()",
            "TrainingCenterSettingsStore.LoadAsync",
            "TrainingCenterRuntimeHelpers.CreateMeterTimelineService",
            "new TrainingSampleGenerator",
            "TrainingSamplesStore.LoadAsync",
            "TrainingCenterSampleGenerationRuntime.GenerateWithDiagnosticsAsync(",
            "TrainingSamplesStore.MergeAndSaveAsync",
            "TrainingSamplesStore.MergeAndSaveAsync(newSamples)",
            "ObservableCollectionContentController.Append(Samples, newSamples)",
            "TrainingCenterSampleGenerationStatusFormatter.FormatEmptyCaseStatus");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_selection_command_refresh_an_controller()
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
            "TrainingSelectionCommandRefreshController.cs");
        var caseChangedSource = ExtractMethodBody(source, "partial void OnSelectedCaseChanged(TrainingCase? value)");
        var sampleChangedSource = ExtractMethodBody(source, "partial void OnSelectedSampleChanged(TrainingSample? value)");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("TrainingSelectionCommandRefreshController.RefreshCaseSelection(", caseChangedSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCaseSelectionCommandRefresh(", caseChangedSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectionCommandRefreshController.RefreshSampleSelection(", sampleChangedSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingSampleSelectionCommandRefresh(", sampleChangedSource, StringComparison.Ordinal);
        Assert.Contains("RemoveSampleCommand.NotifyCanExecuteChanged", sampleChangedSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            caseChangedSource,
            "ApproveCommand.NotifyCanExecuteChanged();",
            "RejectCommand.NotifyCanExecuteChanged();",
            "SetNewCommand.NotifyCanExecuteChanged();",
            "GenerateSamplesCommand.NotifyCanExecuteChanged();");
        AssertNoForbiddenTokens(
            sampleChangedSource,
            "ApproveSampleCommand.NotifyCanExecuteChanged();",
            "RejectSampleCommand.NotifyCanExecuteChanged();",
            "RemoveSampleCommand.NotifyCanExecuteChanged();");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_generation_cancellation_lifecycle_an_helper()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var yoloExportSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.YoloExport.cs"));
        var resetSource = ExtractMethodBody(source, "private CancellationTokenSource ResetGenerationCancellationSource()");
        var resetTokenSource = ExtractMethodBody(source, "private CancellationToken ResetGenerationCancellation()");
        var cancelBatchSource = ExtractMethodBody(source, "private void CancelBatch()");
        var reconcileSource = ExtractMethodBody(source, "private async Task ReconcileGoldToKbAsync()");
        var reconcileCommandSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingGoldKbReconcileCommandWorkflow.cs"));
        var yoloWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));
        var exportYoloSource = ExtractMethodBody(yoloExportSource, "private async Task ExportYoloAsync()");

        Assert.Contains("using AuswertungPro.Next.UI.Player;", source, StringComparison.Ordinal);
        Assert.Contains("_genCts = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_genCts);", resetSource, StringComparison.Ordinal);
        Assert.Contains("return _genCts;", resetSource, StringComparison.Ordinal);
        Assert.Contains("return ResetGenerationCancellationSource().Token;", resetTokenSource, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunControlController.Cancel(", cancelBatchSource, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_genCts)", cancelBatchSource, StringComparison.Ordinal);
        Assert.Contains("ResetCancellation: ResetGenerationCancellation", reconcileSource, StringComparison.Ordinal);
        Assert.Contains("var ct = request.ResetCancellation();", reconcileCommandSource, StringComparison.Ordinal);
        Assert.Contains("ResetGenerationCancellation", exportYoloSource, StringComparison.Ordinal);
        Assert.Contains("var ct = request.ResetCancellation();", yoloWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            resetSource + resetTokenSource + cancelBatchSource + reconcileSource + exportYoloSource,
            "_genCts?.Cancel();",
            "_genCts?.Dispose();",
            "new CancellationTokenSource();");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_lokalen_yolo_export_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloLocalExportWorkflow.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.Contains("TrainingYoloLocalExportWorkflow.RunAsync", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "private async Task ExportYoloLocalAsync(",
            "TeacherAnnotationStore.LoadAsync()",
            "Directory.CreateDirectory(",
            "File.Copy(",
            "File.WriteAllTextAsync(",
            "File.WriteAllLinesAsync(",
            "VsaYoloClassMap.GetClassId(",
            "VsaYoloClassMap.GetFullMap(",
            "VsaYoloClassMap.ExportClassesTxtAsync(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sidecar_yolo_payload_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloSidecarExportPayloadWorkflow.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.Contains("TrainingYoloSidecarExportPayloadWorkflow.BuildAsync", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "File.ReadAllBytesAsync(",
            "new List<TrainingExportSample>()",
            "new List<TrainingExportSampleLabel>",
            "new TrainingExportSample(",
            "new TrainingExportSampleLabel",
            "EvalContaminationGuard.ClassifyForExport(",
            "int skipEvalHash",
            "int skipEvalCase",
            "int skipNoBox");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sidecar_yolo_runtime_an_factory()
    {
        var repoRoot = FindRepoRoot();
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloSidecarRuntimeFactory.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.Contains("TrainingYoloSidecarRuntimeFactory.CreateWithDefaults", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "new AppSettingsAiSettingsProvider()",
            "new VisionPipelineClient(",
            ".ToPipelineConfig()");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_yolo_export_kandidatenauswahl_an_selector()
    {
        var repoRoot = FindRepoRoot();
        var selectorPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportCandidateSelector.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(selectorPath), selectorPath);
        Assert.Contains("TrainingYoloExportCandidateSelector.SelectWithFileSystem(", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "File.Exists(",
            "s.Status == TrainingSampleStatus.Approved",
            "!string.IsNullOrWhiteSpace(s.FramePath)",
            ".Where(IsTrainingExportEligible)",
            "var candidates = Samples");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_yolo_export_zielordner_dialog_an_selector()
    {
        var repoRoot = FindRepoRoot();
        var selectorPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportTargetFolderSelector.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(selectorPath), selectorPath);
        Assert.Contains("TrainingYoloExportTargetFolderSelector.SelectFolder", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "using Microsoft.Win32;",
            "new OpenFolderDialog",
            "ShowDialog()",
            "FolderName");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sidecar_yolo_abschluss_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloSidecarExportCompletionWorkflow.cs");
        var yoloExportWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs"));

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.Contains("TrainingYoloSidecarExportCompletionWorkflow.RunAsync", yoloExportWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingYoloSidecarExportCompletionRequestFactory.CreateWithDefaults(", yoloExportWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            yoloExportWorkflowSource,
            "s.ExportedUtc = DateTime.UtcNow",
            "response.TotalSamples",
            "response.TrainCount",
            "response.ValCount",
            "response.ClassesUsed.Count",
            "response.DataYamlPath",
            "string.Join(\", \", response.ClassesUsed)");
    }

    [Fact]
    public void TrainingCenterViewModel_export_yolo_startet_nur_noch_export_workflow()
    {
        var repoRoot = FindRepoRoot();
        var yoloExportSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.YoloExport.cs"));
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingYoloExportWorkflow.cs");

        var exportYoloSource = ExtractMethodBody(yoloExportSource, "private async Task ExportYoloAsync()");

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.Contains("TrainingYoloExportWorkflow.RunAsync(", exportYoloSource, StringComparison.Ordinal);
        Assert.Contains("TrainingYoloExportRequestFactory.CreateWithDefaults(", exportYoloSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            exportYoloSource,
            "TrainingYoloExportCandidateSelector.SelectWithFileSystem(",
            "TrainingYoloExportTargetFolderSelector.SelectFolder()",
            "TrainingYoloSidecarRuntimeFactory.CreateWithDefaults()",
            ".HealthCheckAsync(",
            ".ExportTrainingAsync(",
            "TrainingYoloSidecarExportPayloadWorkflow.BuildAsync(",
            "TrainingYoloSidecarExportCompletionWorkflow.RunAsync(",
            "TrainingYoloLocalExportWorkflow.RunAsync(",
            "EvalContaminationSetProvider.Load(",
            "RunYoloLocalExportAsync(",
            "catch (OperationCanceledException)",
            "catch (Exception ex)",
            "finally",
            "IsBusy = true",
            "IsBusy = false");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_case_decisions_an_controller()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCaseCommandWorkflow.cs");
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCaseCommandRequestFactory.cs");
        var factorySource = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var approveSource = ExtractMethodBody(source, "private void Approve()");
        var rejectSource = ExtractMethodBody(source, "private void Reject()");
        var setNewSource = ExtractMethodBody(source, "private void SetNew()");

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.Contains("new TrainingCaseCommandWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandRequestFactory.Create(", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandRequestFactory.Create(", rejectSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandRequestFactory.Create(", setNewSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandWorkflow.Run(", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecision.Approve", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandWorkflow.Run(", rejectSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecision.Reject", rejectSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseCommandWorkflow.Run(", setNewSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecision.SetNew", setNewSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            approveSource + rejectSource + setNewSource,
            "new TrainingCaseCommandWorkflowRequest(",
            "if (SelectedCase is null) return;",
            "TrainingCaseDecisionController.Apply(",
            "TrainingCaseDecisionCompletionController.Apply(",
            "SelectedCase.Status = TrainingCaseStatus.Approved",
            "SelectedCase.Status = TrainingCaseStatus.Rejected",
            "SelectedCase.Status = TrainingCaseStatus.New",
            "StatusText = decision.StatusText");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_step_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var stepSource = ExtractMethodBody(source, "public void OnSelfTrainingStep(SelfTrainingStep step)");
        var workflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingStepWorkflow.cs"));

        Assert.Contains("SelfTrainingStepWorkflow.Apply(", stepSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingStepPresentationBuilder.Build(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.Step", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.ActiveVisionModel", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            stepSource,
            "SelfTrainingStepPresentationBuilder.Build(",
            "case SelfTrainingStage.ExtractingFrame",
            "case SelfTrainingStage.Completed",
            "new SelfTrainingEntryResult",
            "CurrentTechniqueGrade = presentation",
            "SelfTrainingResults.Add(",
            "_matchRateTracker.Record(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_match_rate_zaehler_an_tracker()
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
            "SelfTrainingStepWorkflow.cs"));
        var lastMatchRateWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingLastMatchRateRefreshWorkflow.cs"));
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingMatchRatePresentationController.cs");
        var stepSource = source;
        var refreshSource = ExtractMethodBody(source, "private void RefreshMatchRatePercents()");
        var loadSource = ExtractMethodBody(source, "private async Task LoadLastMatchRateAsync()");
        var resetSource = source;

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("private readonly SelfTrainingMatchRateTracker _matchRateTracker = new();", source, StringComparison.Ordinal);
        Assert.Contains("MatchRateTracker: _matchRateTracker", stepSource, StringComparison.Ordinal);
        Assert.Contains("request.MatchRateTracker.Record(level)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("_matchRateTracker.ComputePercents()", refreshSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingMatchRatePresentationController.Apply(", refreshSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(", loadSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingMatchRatePresentationController.Apply(", lastMatchRateWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("_matchRateTracker.Reset", resetSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(source, "_totalExact");
        AssertNoForbiddenTokens(
            refreshSource + loadSource,
            "ExactPercent = p.Exact",
            "PartialPercent = p.Partial",
            "MismatchPercent = p.Mismatch",
            "NoFindingsPercent = p.NoFindings",
            "ExactPercent = presentation.ExactPercent",
            "PartialPercent = presentation.PartialPercent",
            "MismatchPercent = presentation.MismatchPercent",
            "NoFindingsPercent = presentation.NoFindingsPercent");
        AssertNoForbiddenTokens(stepSource, "case MatchLevel.ExactMatch");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_visual_reset_an_controller()
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
            "SelfTrainingVisualResetController.cs");
        var resetSource = source;

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("SelfTrainingVisualResetController.Reset(", resetSource, StringComparison.Ordinal);
        Assert.Contains("new SelfTrainingVisualResetRequest(", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingResults.Clear();", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeDistribution.Clear();", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingLogEntries.Clear();", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PipelineActiveStep = 0;", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentEntryCode = \"\";", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentComparisonText = \"\";", resetSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_code_distribution_an_controller()
    {
        var repoRoot = FindRepoRoot();
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingCodeDistributionController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var distributionSource = ExtractMethodBody(source, "private void UpdateCodeDistribution(string code, MatchLevel level)");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("SelfTrainingCodeDistributionController.ApplyMatchOnUi(", distributionSource, StringComparison.Ordinal);
        Assert.Contains("CodeDistribution,", distributionSource, StringComparison.Ordinal);
        Assert.Contains("OnUi)", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingCodeDistributionController.ApplyMatch(CodeDistribution, code, level)", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("void Apply()", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnUi(Apply)", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeDistribution.FirstOrDefault(e => e.Code == code)", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new CodeDistributionEntry { Code = code }", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeDistribution.Add(entry)", distributionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingStatusCalculator.ApplyMatch(entry, level)", distributionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_samples_load_und_collection_mutation_an_workflow()
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
            "TrainingSampleCollectionController.cs");
        var loadWorkflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSampleLoadWorkflow.cs");
        var loadFactoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingSampleLoadRequestFactory.cs");
        var loadSource = ExtractMethodBody(source, "private async Task LoadSamplesInternalAsync()");
        var generateMethod = ExtractMethodBody(source, "private async Task GenerateSamplesAsync()");
        var batchMethod = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");
        var generationWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterSampleGenerationWorkflow.cs"));
        var batchRunFactorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunRequestFactory.cs"));

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.True(File.Exists(loadWorkflowPath), loadWorkflowPath);
        Assert.True(File.Exists(loadFactoryPath), loadFactoryPath);
        var loadWorkflowSource = File.ReadAllText(loadWorkflowPath);
        var loadFactorySource = File.ReadAllText(loadFactoryPath);

        Assert.Contains("TrainingSampleLoadWorkflow.RunAsync(", loadSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleLoadRequestFactory.CreateWithDefaults(", loadSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", loadFactorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleCollectionController.ReplaceOnUi(", loadWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("AppendSamples: samples => TrainingSampleCollectionController.Append(Samples, samples)", generateMethod, StringComparison.Ordinal);
        Assert.Contains("ReplaceSamples: items => TrainingSampleCollectionController.ReplaceWith(request.Samples, items)", batchRunFactorySource, StringComparison.Ordinal);
        Assert.Contains("request.AppendSamples(newSamples)", generationWorkflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            loadSource,
            "new TrainingSampleLoadWorkflowRequest(",
            "TrainingSamplesStore.LoadAsync",
            "TrainingSamplesStore.LoadAsync()",
            "TrainingSampleCollectionController.ReplaceOnUi(");
        AssertNoForbiddenTokens(
            source,
            "ObservableCollectionContentController.ReplaceWith(Samples",
            "ObservableCollectionContentController.Append(Samples",
            "Samples.Clear()",
            "foreach (var s in list)");
        AssertNoForbiddenTokens(generateMethod, "foreach (var s in newSamples)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_command_und_run_request_erzeugung_an_factory()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportCommandRequestFactory.cs");
        var runFactoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingBatchImportRunRequestFactory.cs");
        var batchMethod = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");
        var factorySource = File.ReadAllText(factoryPath);
        var runFactorySource = File.ReadAllText(runFactoryPath);

        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.True(File.Exists(runFactoryPath), runFactoryPath);
        Assert.Contains("TrainingBatchImportCommandRequestFactory.CreateWithDefaults(", batchMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportCommandRunDefaultRequestFactoryRequest(", batchMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingBatchImportRunRequestFactory.CreateWithDefaults(", factorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingBatchImportRunDefaultRequestFactoryRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingPreviewFrameExtractor.ExtractPreviewFrameAsync", runFactorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            batchMethod,
            "new TrainingBatchImportRunWorkflowRequest(",
            "TrainingBatchImportRunWorkflow.RunAsync(",
            "TrainingBatchImportRunRequestFactory.CreateWithDefaults(",
            "new TrainingBatchImportRunDefaultRequestFactoryRequest(",
            "RunImportAsync: async ct =>",
            "TrainingCenterRuntimeHelpers.ToTrainingCase",
            "Select(TrainingCenterRuntimeHelpers.ToTrainingCase)",
            "TrainingCenterRuntimeHelpers.ExtractPreviewFrameAsync",
            "ExtractPreviewFrameAsync:",
            "DirectoryExists: Directory.Exists",
            "LoadRuntimeSettings: () => PlayerAiSettingsLoader.LoadRuntimeSettings()",
            "LoadSettingsAsync: TrainingCenterSettingsStore.LoadAsync",
            "LoadSamplesAsync: TrainingSamplesStore.LoadAsync",
            "MergeAndSaveSamplesAsync: TrainingSamplesStore.MergeAndSaveAsync",
            "BeginActivity: () => AiTrack.Begin(\"Training Center\")",
            "RunWorkflowAsync: TrainingBatchImportWorkflow.RunAsync");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_cases_restore_collection_mutation_an_controller()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = ExtractMethodBody(source, "public async Task LoadAsync()");
        var workflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterLoadWorkflow.cs"));

        Assert.Contains("ReplaceCases: items => ObservableCollectionContentController.ReplaceWith(Cases, items)", loadSource, StringComparison.Ordinal);
        Assert.Contains("request.ReplaceCases(state.Cases)", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            loadSource,
            "ObservableCollectionContentController.ReplaceWith(Cases, state.Cases)",
            "Cases.Clear()",
            "foreach (var c in state.Cases)",
            "Cases.Add(c)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_rootfolder_mutation_an_state_controller()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = ExtractMethodBody(source, "public async Task LoadAsync()");
        var browseSource = ExtractMethodBody(source, "private void BrowseRootFolder()");
        var clearSource = ExtractMethodBody(source, "private void ClearRootFolders()");
        var workflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterDistributionWorkflow.cs"));
        var rootFolderWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterRootFolderWorkflow.cs"));
        var rootFolderDialogSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterRootFolderDialogSelector.cs"));
        var loadWorkflowSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterLoadWorkflow.cs"));

        Assert.Contains("RootFolders: _rootFolders", loadSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.ReplaceRootFolders(request.RootFolders, restoredRootFolders)", loadWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterRootFolderDialogSelector.SelectFolders()", browseSource, StringComparison.Ordinal);
        Assert.Contains("new OpenFolderDialog", rootFolderDialogSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterRootFolderWorkflow.ApplySelected(", browseSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterRootFolderWorkflow.Clear(", clearSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.AddSelectedRootFolders(", rootFolderWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.ReplaceRootFolders(", rootFolderWorkflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.AddRootFolder(request.RootFolders, result.OutputFolder)", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            browseSource,
            "new OpenFolderDialog",
            "TrainingCenterStateController.AddSelectedRootFolders(");
        AssertNoForbiddenTokens(
            clearSource,
            "TrainingCenterStateController.ReplaceRootFolders(",
            "Array.Empty<string>()");
        AssertNoForbiddenTokens(
            source,
            "_rootFolders.Clear()",
            "_rootFolders.Add(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_haltungs_verteilung_an_workflow()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var distributionSource = ExtractMethodBody(source, "private async Task DistributeHaltungAsync()");
        var factorySource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterDistributionRequestFactory.cs"));
        var dialogSource = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterDistributionDialogSelector.cs"));

        Assert.Contains("TrainingCenterDistributionWorkflow.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterDistributionRequestFactory.CreateWithDefaultSelectors(", distributionSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterDistributionWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterDistributionDialogSelector.SelectPdfPath", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterDistributionDialogSelector.SelectVideoFolder", factorySource, StringComparison.Ordinal);
        Assert.Contains("new OpenFileDialog", dialogSource, StringComparison.Ordinal);
        Assert.Contains("new OpenFolderDialog", dialogSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            distributionSource,
            "new TrainingCenterDistributionWorkflowRequest(",
            "TrainingCenterDistributionDialogSelector.SelectPdfPath",
            "TrainingCenterDistributionDialogSelector.SelectVideoFolder",
            "new OpenFileDialog",
            "new OpenFolderDialog");
        AssertNoForbiddenTokens(
            viewModelSource,
            "Path.GetFileNameWithoutExtension(pdfPath)",
            "PDF nach Haltungen aufteilen...",
            "foreach (var msg in result.Messages)",
            "Chunks ohne Haltungs-ID uebersprungen",
            "Output-Ordner als Trainings-Ordner hinzugefuegt");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_scan_an_workflow()
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
            "TrainingCenterScanWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterScanRequestFactory.cs"));
        var scanSource = ExtractMethodBody(source, "private async Task ScanAsync()");

        Assert.Contains("TrainingCenterScanWorkflow.RunAsync(", scanSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterScanRequestFactory.CreateWithDefaults(", scanSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterScanDefaultRequestFactoryRequest(", scanSource, StringComparison.Ordinal);
        Assert.Contains("Directory.Exists", factorySource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterScanWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseInputMapper.ToTrainingCase", factorySource, StringComparison.Ordinal);
        Assert.Contains("request.ScanInputsAsync(folder)", factorySource, StringComparison.Ordinal);
        Assert.Contains("Select(request.ToTrainingCase)", factorySource, StringComparison.Ordinal);
        Assert.Contains("request.ReplaceCases(Array.Empty<TrainingCase>())", workflowSource, StringComparison.Ordinal);
        Assert.Contains("request.AppendCases(found)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterDisplayFormatter.FormatScanSummary(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("await request.SaveStateAsync()", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(workflowSource, ".ConfigureAwait(false)");
        AssertNoForbiddenTokens(
            scanSource,
            "new TrainingCenterScanWorkflowRequest(",
            "_import.ScanAsync(folder)",
            "TrainingCenterRuntimeHelpers.ToTrainingCase",
            "Select(TrainingCenterRuntimeHelpers.ToTrainingCase)",
            "ObservableCollectionContentController.ReplaceWith(Cases, Array.Empty<TrainingCase>())",
            "ObservableCollectionContentController.Append(",
            "foreach (var folder in _rootFolders)",
            "Directory.Exists",
            "Directory.Exists(folder)",
            "TrainingCenterDisplayFormatter.FormatScanSummary(Cases.Count",
            "await AutoSaveStateAsync()");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_manuelles_speichern_an_workflow()
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
            "TrainingCenterSaveWorkflow.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterSaveRequestFactory.cs"));
        var autoSaveSource = ExtractMethodBody(source, "private async Task AutoSaveStateAsync()");
        var batchMethod = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");
        var saveSource = ExtractMethodBody(source, "private async Task SaveAsync()");

        Assert.Contains("TrainingCenterSaveWorkflow.RunAsync(", saveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterSaveRequestFactory.CreateWithDefaults(", saveSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterSaveDefaultRequestFactoryRequest(", saveSource, StringComparison.Ordinal);
        Assert.Contains("new TrainingCenterSaveWorkflowRequest(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.BuildState(", factorySource, StringComparison.Ordinal);
        Assert.Contains("DateTime.UtcNow", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterSaveRequestFactory.BuildStateWithDefaults(", autoSaveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterSaveRequestFactory.BuildStateWithDefaults(", batchMethod, StringComparison.Ordinal);
        Assert.Contains("await request.SaveStateAsync(state)", workflowSource, StringComparison.Ordinal);
        Assert.Contains("Gespeichert: {state.Cases.Count} Fälle, {state.RootFolders.Count} Ordner", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            saveSource,
            "new TrainingCenterSaveWorkflowRequest(",
            "TrainingCenterSaveRequestFactory.Create(",
            "new TrainingCenterSaveRequestFactoryRequest(",
            "BuildState:",
            "DateTime.UtcNow",
            "if (IsBusy) return;",
            "IsBusy = true;",
            "IsBusy = false;",
            "_store.SaveAsync(BuildState())",
            "StatusText = $\"Gespeichert:");
        AssertNoForbiddenTokens(
            autoSaveSource + batchMethod,
            "BuildState()",
            "DateTime.UtcNow",
            "TrainingCenterStateController.BuildState(");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_log_format_und_trim_an_controller()
    {
        var repoRoot = FindRepoRoot();
        var controllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingCenterLogController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingLogSource = ExtractMethodBody(source, "private void AddSelfTrainingLog(string message)");
        var logSource = ExtractMethodBody(source, "private void Log(string message)");
        var appendSource = source;

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.Contains("TrainingCenterLogController.AppendSelfTrainingLog(", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterLogController.AppendLog(", logSource, StringComparison.Ordinal);
        Assert.Contains("DateTime.Now", File.ReadAllText(controllerPath), StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", selfTrainingLogSource + logSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterLogController.FormatEntry(message, DateTime.Now)", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterLogController.FormatEntry(message, DateTime.Now)", logSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterLogController.AppendSelfTrainingEntry(SelfTrainingLogEntries, entryText)", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterLogController.AppendLogText(LogText, entryText)", logSource, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"[{DateTime.Now:HH:mm:ss}] {message}\"", logSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LogText += entryText + \"\\n\";", logSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingLogEntries.Count > 100", appendSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingLogEntries.RemoveAt(0)", appendSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_live_frame_throttling_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var throttleSource = ExtractMethodBody(source, "private void SetLiveFrameThrottled(string? path)");

        Assert.Contains("TrainingLiveFrameThrottleController.Apply(", throttleSource, StringComparison.Ordinal);
        Assert.Contains("() => _lastLiveFrameUpdate", throttleSource, StringComparison.Ordinal);
        Assert.Contains("value => _lastLiveFrameUpdate = value", throttleSource, StringComparison.Ordinal);
        Assert.Contains("value => LiveFramePath = value", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.UtcNow", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingLiveFrameThrottleController.Decide(", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!decision.ShouldUpdateFramePath) return;", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveFramePath = decision.FramePath", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_lastLiveFrameUpdate = decision.LastUpdatedUtc", throttleSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            throttleSource,
            "TotalMilliseconds < 180",
            "string.IsNullOrEmpty(path)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_live_preview_clear_an_controller()
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
            "TrainingLivePreviewClearController.cs");
        var applyControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingLivePreviewApplyController.cs");
        var updateSource = ExtractMethodBody(source, "private void UpdateLivePreview(");
        var clearSource = ExtractMethodBody(source, "private void ClearLivePreview()");

        Assert.True(File.Exists(controllerPath), controllerPath);
        Assert.True(File.Exists(applyControllerPath), applyControllerPath);
        Assert.Contains("TrainingLivePreviewApplyController.ApplyOnUi(", updateSource, StringComparison.Ordinal);
        Assert.Contains("TrainingLivePreviewClearController.ApplyOnUi(", clearSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            updateSource,
            "void Apply()",
            "OnUi(Apply)",
            "TrainingLivePreviewPresenter.Build(",
            "TrainingLivePreviewApplyController.Apply(",
            "LiveCaseInfo = preview.LiveCaseInfo",
            "LiveCodeInfo = preview.LiveCodeInfo",
            "LiveMeterInfo = preview.LiveMeterInfo",
            "CurrentComparisonText = preview.CurrentComparisonText",
            "CurrentEntryCode = preview.CurrentEntryCode",
            "if (preview.FramePath is not null)",
            "SetLiveFrameThrottled(preview.FramePath)",
            "string.IsNullOrEmpty(LiveFramePath)",
            "LiveFramePath = \"\"");
        AssertNoForbiddenTokens(
            clearSource,
            "TrainingLivePreviewClearController.Apply(",
            "var preview = TrainingLivePreviewPresenter.Clear();",
            "LiveCaseInfo = preview.LiveCaseInfo",
            "LiveCodeInfo = preview.LiveCodeInfo",
            "LiveMeterInfo = preview.LiveMeterInfo",
            "CurrentComparisonText = preview.CurrentComparisonText",
            "CurrentEntryCode = preview.CurrentEntryCode",
            "SetLiveFrameThrottled(preview.FramePath)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_protocol_startdata_queue_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var completionControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataQueueCompletionController.cs");
        var catalogControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataCatalogController.cs");
        var reloadControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingReviewQueueReloadController.cs");
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataSuggestionWorkflow.cs");
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataSuggestionRequestFactory.cs");
        var viewModelSource = source;
        var suggestMethod = ExtractMethodBody(source, "private async Task SuggestProtocolStartdataAsync()");
        var reloadMethod = ExtractMethodBody(source, "private void ReloadCurrentReviewQueue()");

        Assert.True(File.Exists(completionControllerPath), completionControllerPath);
        Assert.True(File.Exists(catalogControllerPath), catalogControllerPath);
        Assert.True(File.Exists(reloadControllerPath), reloadControllerPath);
        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.True(File.Exists(factoryPath), factoryPath);
        var workflowSource = File.ReadAllText(workflowPath);
        var factorySource = File.ReadAllText(factoryPath);

        Assert.Contains("TrainingProtocolStartdataSuggestionWorkflow.RunAsync(", suggestMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataSuggestionRequestFactory.CreateWithDefaults(", suggestMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingProtocolStartdataSuggestionRequestFactoryRequest(", suggestMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", factorySource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeResolver.CurrentCatalog", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataQueueController.Run(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataCatalogController.Resolve(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataCatalogController.EnsureAvailable(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataQueueCompletionController.Apply(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewQueueReloadController.Reload(", reloadMethod, StringComparison.Ordinal);
        Assert.Contains("ReviewQueueServiceRef", reloadMethod, StringComparison.Ordinal);
        Assert.Contains("LoadReviewQueue", reloadMethod, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            viewModelSource,
            "ProtocolReviewCandidateFilter.SelectCandidates",
            "ReviewQueueServiceRef.GetAll()",
            "ReviewQueueServiceRef.EnqueueFromSelfTraining");
        AssertNoForbiddenTokens(
            suggestMethod,
            "new TrainingProtocolStartdataSuggestionWorkflowRequest(",
            "TrainingSamplesStore.LoadAsync",
            "TrainingProtocolStartdataQueueController.Run(",
            "TrainingProtocolStartdataCatalogController.Resolve(",
            "TrainingProtocolStartdataCatalogController.EnsureAvailable(",
            "TrainingProtocolStartdataQueueCompletionController.Apply(",
            "var all = await TrainingSamplesStore.LoadAsync()",
            "LoadReviewQueue(ReviewQueueServiceRef)",
            "OnUi(() => ReviewStatusText = result.StatusText)",
            "Log(result.LogText)",
            "_codeCatalog ??",
            "if (catalog is null)",
            "Kein Code-Katalog verfuegbar.");
        AssertNoForbiddenTokens(
            reloadMethod,
            "if (ReviewQueueServiceRef is not null)",
            "LoadReviewQueue(ReviewQueueServiceRef)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_protocol_startdata_approval_an_workflow()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var completionControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataApprovalCompletionController.cs");
        var workflowPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataApprovalWorkflow.cs");
        var viewModelSource = source;
        var startdataMethod = ExtractMethodBody(source, "public async Task ApproveAllStartdataAsync(");

        Assert.True(File.Exists(completionControllerPath), completionControllerPath);
        Assert.True(File.Exists(workflowPath), workflowPath);
        var factoryPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingProtocolStartdataApprovalRequestFactory.cs");
        Assert.True(File.Exists(factoryPath), factoryPath);
        var workflowSource = File.ReadAllText(workflowPath);
        var factorySource = File.ReadAllText(factoryPath);

        Assert.Contains("TrainingProtocolStartdataApprovalWorkflow.RunAsync(", startdataMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataApprovalRequestFactory.CreateWithDefaults(", startdataMethod, StringComparison.Ordinal);
        Assert.Contains("new TrainingProtocolStartdataApprovalRequestFactoryRequest(", startdataMethod, StringComparison.Ordinal);
        Assert.Contains("TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataApprovalController.ApproveAllAsync(", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataApprovalCompletionController.Apply(", workflowSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            startdataMethod,
            "new TrainingProtocolStartdataApprovalWorkflowRequest(",
            "TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(",
            "TrainingProtocolStartdataApprovalController.ApproveAllAsync(",
            "TrainingProtocolStartdataApprovalCompletionController.Apply(",
            "var result = await",
            "foreach (var errorLog in result.ErrorLogTexts)",
            "Log(errorLog)",
            "OnUi(() => ReviewStatusText = result.StatusText)");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_protocol_startdata_review_item_filter_an_selector()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var countSource = ExtractPropertyBody(source, "public int StartdataCandidateCount");
        var viewModelSource = source;
        var getItemsSource = ExtractMethodBody(source, "private List<InfraSelfImproving.ReviewQueueItem> GetProtocolStartdataReviewItems()");

        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.Count(ReviewQueue)", countSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.SelectOnUi(ReviewQueue, OnUi)", getItemsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingProtocolStartdataReviewItemSelector.Select(ReviewQueue)", getItemsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("List<InfraSelfImproving.ReviewQueueItem>? items = null", getItemsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnUi(() =>", getItemsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("items ?? new List<InfraSelfImproving.ReviewQueueItem>()", getItemsSource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(countSource, "SelfTrainingMatchLevel");
        AssertNoForbiddenTokens(viewModelSource, "SelfTrainingMatchLevel.ProtocolStartdata");
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_kb_update_workflow_an_controller()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;
        var factorySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunRequestFactory.cs"));

        Assert.Contains("SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(", factorySource, StringComparison.Ordinal);
        Assert.Contains("request.IndexSamplesAsync", factorySource, StringComparison.Ordinal);
        AssertNoForbiddenTokens(
            viewModelSource,
            "SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(",
            "SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(allSamples, result)",
            "SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)",
            "SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)");
    }

    [Fact]
    public void TrainingKbIndexRunner_haengt_nicht_am_viewmodel_runtime_helper()
    {
        var repoRoot = FindRepoRoot();
        var runnerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingKbIndexRunner.cs"));
        var reachabilityPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingOllamaReachabilityChecker.cs");

        Assert.True(File.Exists(reachabilityPath), reachabilityPath);
        var reachabilitySource = File.ReadAllText(reachabilityPath);
        Assert.Contains("TrainingOllamaReachabilityChecker.CheckAsync", runnerSource, StringComparison.Ordinal);
        Assert.Contains("CheckAsync(OllamaConfig", reachabilitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("AuswertungPro.Next.UI.ViewModels.Windows", runnerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterRuntimeHelpers", runnerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfTrainingSessionController_nutzt_ffmpeg_resolver_aus_training_paket()
    {
        var repoRoot = FindRepoRoot();
        var sessionControllerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingSessionController.cs"));
        var newResolverPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "TrainingFfmpegPathResolver.cs");
        var oldResolverPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingFfmpegPathResolver.cs");

        Assert.True(File.Exists(newResolverPath), newResolverPath);
        Assert.False(File.Exists(oldResolverPath), oldResolverPath);
        var resolverSource = File.ReadAllText(newResolverPath);
        Assert.Contains("namespace AuswertungPro.Next.UI.Ai.Training", resolverSource, StringComparison.Ordinal);
        Assert.Contains("TrainingFfmpegPathResolver.Resolve", sessionControllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AuswertungPro.Next.UI.ViewModels.Windows", sessionControllerSource, StringComparison.Ordinal);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte TrainingCenter-SelfTraining-Logik gefunden: " + string.Join(", ", hits));
    }

    private static string ExtractPropertyBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Property nicht gefunden: {signature}");

        var semicolonIndex = source.IndexOf(';', signatureIndex);
        Assert.True(semicolonIndex > signatureIndex, $"Property-Ende nicht gefunden: {signature}");

        return source[signatureIndex..(semicolonIndex + 1)];
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
