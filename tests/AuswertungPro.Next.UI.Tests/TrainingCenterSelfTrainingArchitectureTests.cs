using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSelfTrainingArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_run_preparation_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var preparationControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunPreparationController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(preparationControllerPath), "Triviale Self-Training-CTS-Vorbereitung soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunPreparationController.PrepareCancellation(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Cancel();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Dispose();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts = new CancellationTokenSource();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var ct = _selfTrainingCts.Token;", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_auto_scan_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("SelfTrainingAutoScanController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.ShouldScan(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.ScanAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.StatusText", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (Cases.Count == 0 && _rootFolders.Count > 0)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var c in autoScannedCases)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Add(c);", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_case_selection_orchestrierung_inline()
    {
        var repoRoot = FindRepoRoot();
        var workflowControllerPath = Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingCaseSelectionWorkflowController.cs");
        var source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.False(File.Exists(workflowControllerPath), workflowControllerPath);
        Assert.DoesNotContain("SelfTrainingCaseSelectionWorkflowController", source, StringComparison.Ordinal);
        Assert.Contains("if (SelectedCase is null)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("existingSamplesForSelection = await TrainingSamplesStore.LoadAsync();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingCaseSelectionController.Select(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelectedCase = selectedCase;", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingCaseSelectionController.WithProtocolOrStop", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_review_queue_orchestrierung_inline()
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
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.False(File.Exists(workflowControllerPath), workflowControllerPath);
        Assert.DoesNotContain("SelfTrainingReviewQueueWorkflowController", source, StringComparison.Ordinal);
        Assert.Contains("ReviewQueueServiceRef is not null", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingReviewCandidateSelector.HasReviewableMatches(result)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var reviewSamples = await TrainingSamplesStore.LoadAsync();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingReviewQueueController.EnqueueCandidates(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("LoadReviewQueue(ReviewQueueServiceRef);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Log(reviewQueueUpdate.LogMessage ?? \"\");", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.EnqueueFromSelfTraining(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingReviewCandidateSelector.SelectForRun(allSamplesForReview, result)", viewModelSource, StringComparison.Ordinal);
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
        var viewModelSource = source;

        Assert.Contains("SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ShouldRun(result)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("s.KbIndexState is KbIndexState.None or KbIndexState.Error", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("stOutcome.IndexedIds.ToHashSet()", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_self_training_startzustand_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var startControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunStartController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(startControllerPath), "Trivialer Self-Training-Startzustand soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunStartController.Apply(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetBusy(true);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetSelfTrainingRunning(true);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ResetSelfTrainingVisuals(resetMatchRate: true);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetLogText(\"\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildStart(selectedCase)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetStatusText(startPresentation.StatusText);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in startPresentation.LogLines)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog()", viewModelSource, StringComparison.Ordinal);
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
        var viewModelSource = source;

        Assert.Contains("SelfTrainingRuntimeSetupController.PrepareAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(", setupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_completion_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var completionControllerPath = Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Ai",
            "Training",
            "SelfTrainingRunCompletionController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(completionControllerPath), "Triviale Self-Training-Completion-Sequenz soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunCompletionController.Apply(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildCompletion(result)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in completionPresentation.LogLines)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetStatusText(completionPresentation.StatusText);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(result)", viewModelSource, StringComparison.Ordinal);
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
        var viewModelSource = source;

        Assert.Contains("SelfTrainingRuntimeSetupController.PrepareAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("using var selfTrainingSetup", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingSessionController.Create(", setupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingSessionController.Create(", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_run_execution_inline()
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
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.DoesNotContain("SelfTrainingRunExecutionController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingSetup.Session.Orchestrator.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistorySnapshotBuilder.Build(result, DateTime.UtcNow)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistoryStore.AppendRunAsync(snapshot)", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_self_training_post_run_refresh_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("await LoadSamplesInternalAsync();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("await RefreshKbStatusAsync();", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingPostRunRefreshController.RefreshAsync(", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_last_match_rate_presentation_an_builder()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("SelfTrainingLastMatchRatePresentationBuilder.Build(runs)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runs[^1]", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExactPercent = last.ExactPercent", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PartialPercent = last.PartialPercent", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MismatchPercent = last.MismatchPercent", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NoFindingsPercent = last.NoFindingsPercent", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_exceptions_inline()
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
            "SelfTrainingRunExceptionController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Self-Training-Exception-UI soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunExceptionController.ApplyCanceled(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingRunExceptionController.ApplyFailure(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Selbsttraining abgebrochen.\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining abgebrochen.\";", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Log($\"FEHLER: {ex.GetType().Name}: {ex.Message}\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Fehler: {ex.Message}\";", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_self_training_final_state_inline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("selfTrainingUi.SetBusy(false);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetSelfTrainingRunning(false);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator = null;", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingRunFinalizerController.Apply(", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_triviale_self_training_run_control_inline()
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
            "SelfTrainingRunControlController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Triviale Self-Training-Run-Control soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunControlController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Cancel();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining wird abgebrochen...\";", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("if (_selfTrainingOrchestrator is null) return;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("if (_selfTrainingOrchestrator.IsPaused)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator.Resume();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining fortgesetzt.\";", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Pipeline fortgesetzt.\");", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator.Pause();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining pausiert.\";", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Pipeline pausiert.\");", viewModelSource, StringComparison.Ordinal);
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

        Assert.Contains("TrainingKbIndexRunner.CreateDefault(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("runner.RunAsync(samples, ct)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var sample in samples)", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_review_sample_id_aufloesung_an_resolver()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("SelfTrainingReviewSampleIdResolver.ResolveAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Abs", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_gold_kb_reconcile_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingGoldKbReconcileWorkflowController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.MergeOrUpdateAsync", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("IncrementalKbUpdateWithReasonAsync", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbReconcilePlanner.SelectPending", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("const int batchSize", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var s in batch)", viewModelSource, StringComparison.Ordinal);
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
        var viewModelSource = source;

        Assert.Contains("TrainingReviewQueueCompletionController.ApplyApproved(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewQueueCompletionController.ApplyRejected(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("queueService.Remove(item.Id);", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueue.Remove(item);", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_review_queue_load_inline()
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
            "TrainingReviewQueueLoadController.cs");
        var viewModelSource = source;

        Assert.False(File.Exists(controllerPath), "Trivialer Review-Queue-Load soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingReviewQueueLoadController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ArgumentNullException.ThrowIfNull(queueService);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("var items = queueService.GetAll();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueue.Clear();", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in items)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueue.Add(item);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueueCount = items.Count;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ReviewStatusText = $\"{ReviewQueueCount} Einträge zur Prüfung\";", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_check_run_state_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingKnowledgeBaseCheckRunController.TryStart(IsBusy)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplySuccess(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplyFailure(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("summary.LatestVersionAtUtc.Value.ToLocalTime()", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("summary.TopCodes.Count > 0", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KB-Stand: Samples=", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_kb_status_und_quality_presentation_an_builder()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingKnowledgeBaseStatusPresentationBuilder.Build(status)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("status.SampleCount switch", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TakeLast(5)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("quality.StaleSampleCount > 0", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sample_persistenz_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingSamplePersistenceWorkflowController.PersistAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("changedSample.KbIndexState = KbIndexState.Pending", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("outcome.IndexedIds.Contains", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample>", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_approved_protocol_export_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingApprovedProtocolExportController.RunAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuswertungPro.Next.Domain.Protocol.ProtocolEntry", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("approved.Select(s => s.Code)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("s.ExportedUtc = DateTime.UtcNow", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_sample_decisions_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingSampleDecisionController.Approve(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Reject(", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Remove(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Approved", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Rejected", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Removed", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.KbIndexState = KbIndexState.None", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_case_decisions_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.Approve)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.Reject)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.SetNew)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedCase.Status = TrainingCaseStatus.Approved", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedCase.Status = TrainingCaseStatus.Rejected", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedCase.Status = TrainingCaseStatus.New", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_step_presentation_an_builder()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var stepSource = source;

        Assert.Contains("SelfTrainingStepPresentationBuilder.Build(step, _activeVisionModel)", stepSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case SelfTrainingStage.ExtractingFrame", stepSource, StringComparison.Ordinal);
        Assert.DoesNotContain("case SelfTrainingStage.Completed", stepSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelfTrainingEntryResult", stepSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentTechniqueGrade = tech.OverallGrade", stepSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_match_rate_zaehler_an_tracker()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var stepSource = source;
        var refreshSource = source;
        var resetSource = source;

        Assert.Contains("private readonly SelfTrainingMatchRateTracker _matchRateTracker = new();", source, StringComparison.Ordinal);
        Assert.Contains("_matchRateTracker.Record(level)", stepSource, StringComparison.Ordinal);
        Assert.Contains("_matchRateTracker.ComputePercents()", refreshSource, StringComparison.Ordinal);
        Assert.Contains("_matchRateTracker.Reset()", resetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_totalExact", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case MatchLevel.ExactMatch", stepSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_trivialen_self_training_visual_reset_inline()
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

        Assert.False(File.Exists(controllerPath), "Trivialer Self-Training-Visual-Reset soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingVisualResetController", resetSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingResults.Clear();", resetSource, StringComparison.Ordinal);
        Assert.Contains("CodeDistribution.Clear();", resetSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLogEntries.Clear();", resetSource, StringComparison.Ordinal);
        Assert.Contains("PipelineActiveStep = 0;", resetSource, StringComparison.Ordinal);
        Assert.Contains("CurrentEntryCode = \"\";", resetSource, StringComparison.Ordinal);
        Assert.Contains("CurrentEntryMeter = 0;", resetSource, StringComparison.Ordinal);
        Assert.Contains("CurrentComparisonText = \"\";", resetSource, StringComparison.Ordinal);
        Assert.Contains("CurrentTechniqueGrade = \"\";", resetSource, StringComparison.Ordinal);
        Assert.Contains("CurrentTechniqueDetails = \"\";", resetSource, StringComparison.Ordinal);
        Assert.Contains("if (resetMatchRate)", resetSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_code_distribution_inline()
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
        var distributionSource = source;

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.DoesNotContain("SelfTrainingCodeDistributionController", source, StringComparison.Ordinal);
        Assert.Contains("CodeDistribution.FirstOrDefault(e => e.Code == code)", distributionSource, StringComparison.Ordinal);
        Assert.Contains("new CodeDistributionEntry { Code = code }", distributionSource, StringComparison.Ordinal);
        Assert.Contains("CodeDistribution.Add(entry)", distributionSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingStatusCalculator.ApplyMatch(entry, level)", distributionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_samples_collection_mutation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = source;
        var generateSource = source;

        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Samples, list)", loadSource, StringComparison.Ordinal);
        Assert.Contains("ObservableCollectionContentController.Append(Samples, newSamples)", generateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Samples.Clear()", loadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var s in list)", loadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var s in newSamples)", generateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_cases_restore_collection_mutation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = source;

        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Cases, state.Cases)", loadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Clear()", loadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var c in state.Cases)", loadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Add(c)", loadSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_rootfolder_mutation_an_state_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var loadSource = source;
        var clearSource = source;
        var distributeSource = source;

        Assert.Contains("TrainingCenterStateController.ReplaceRootFolders(_rootFolders, restoredRootFolders)", loadSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.ReplaceRootFolders(_rootFolders, Array.Empty<string>())", clearSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCenterStateController.AddRootFolder(_rootFolders, outputFolder)", distributeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_rootFolders.Clear()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_rootFolders.Add(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_scan_cases_collection_mutation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var scanSource = source;

        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Cases, Array.Empty<TrainingCase>())", scanSource, StringComparison.Ordinal);
        Assert.Contains("ObservableCollectionContentController.Append(", scanSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Clear()", scanSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Add(c)", scanSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_setzt_log_format_und_trim_inline()
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
        var selfTrainingLogSource = source;
        var logSource = source;
        var appendSource = source;

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.DoesNotContain("TrainingCenterLogController", source, StringComparison.Ordinal);
        Assert.Contains("var entryText = $\"[{DateTime.Now:HH:mm:ss}] {message}\";", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.Contains("AppendSelfTrainingLogEntry(entryText);", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.Contains("var entryText = $\"[{DateTime.Now:HH:mm:ss}] {message}\";", logSource, StringComparison.Ordinal);
        Assert.Contains("LogText += entryText + \"\\n\";", logSource, StringComparison.Ordinal);
        Assert.Contains("AppendSelfTrainingLogEntry(entryText);", logSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLogEntries.Count > 100", appendSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLogEntries.RemoveAt(0)", appendSource, StringComparison.Ordinal);
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
        var throttleSource = source;

        Assert.Contains("TrainingLiveFrameThrottleController.Decide(path, _lastLiveFrameUpdate, DateTime.UtcNow)", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalMilliseconds < 180", throttleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrEmpty(path)", throttleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_protocol_startdata_queue_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingProtocolStartdataQueueController.Run(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProtocolReviewCandidateFilter.SelectCandidates", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.GetAll()", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.EnqueueFromSelfTraining", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_protocol_startdata_approval_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("TrainingProtocolStartdataApprovalController.ApproveAllAsync(", viewModelSource, StringComparison.Ordinal);
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

        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.Count(ReviewQueue)", countSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.Select(ReviewQueue)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingMatchLevel", countSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingMatchLevel.ProtocolStartdata", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_kb_update_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var viewModelSource = source;

        Assert.Contains("SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(allSamples, result)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)", viewModelSource, StringComparison.Ordinal);
    }

    private static string ExtractPropertyBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Property nicht gefunden: {signature}");

        var semicolonIndex = source.IndexOf(';', signatureIndex);
        Assert.True(semicolonIndex > signatureIndex, $"Property-Ende nicht gefunden: {signature}");

        return source[signatureIndex..(semicolonIndex + 1)];
    }
}
