using System.IO;

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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.False(File.Exists(preparationControllerPath), "Triviale Self-Training-CTS-Vorbereitung soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunPreparationController.PrepareCancellation(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Cancel();", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Dispose();", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts = new CancellationTokenSource();", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("var ct = _selfTrainingCts.Token;", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingAutoScanController.RunAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.ShouldScan(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.ScanAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingAutoScanController.StatusText", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (Cases.Count == 0 && _rootFolders.Count > 0)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var c in autoScannedCases)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Cases.Add(c);", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_case_selection_workflow_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingCaseSelectionWorkflowController.RunAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingCaseSelectionController.Select(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var existingSamplesForSelection", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedCase is null", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("await TrainingSamplesStore.LoadAsync()", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_review_queue_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingReviewQueueWorkflowController.RunAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.EnqueueFromSelfTraining(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingReviewQueueController.EnqueueCandidates(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingReviewCandidateSelector.SelectForRun(allSamplesForReview, result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingReviewCandidateSelector.HasReviewableMatches(result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadReviewQueue(ReviewQueueServiceRef)", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ShouldRun(result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("s.KbIndexState is KbIndexState.None or KbIndexState.Error", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("stOutcome.IndexedIds.ToHashSet()", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.False(File.Exists(startControllerPath), "Trivialer Self-Training-Startzustand soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunStartController.Apply(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetBusy(true);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetSelfTrainingRunning(true);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("ResetSelfTrainingVisuals(resetMatchRate: true);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetLogText(\"\");", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildStart(selectedCase)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetStatusText(startPresentation.StatusText);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in startPresentation.LogLines)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog()", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsBusy = true;", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsSelfTrainingRunning = true;", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LogText = \"\";", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Selbsttraining: {selectedCase.CaseId}", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("--- Selbsttraining starten: {selectedCase.CaseId} ---", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Pipeline gestartet: OSD-Scan", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingRuntimeSetupController.PrepareAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(", setupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.False(File.Exists(completionControllerPath), "Triviale Self-Training-Completion-Sequenz soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunCompletionController.Apply(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildCompletion(result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var line in completionPresentation.LogLines)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetStatusText(completionPresentation.StatusText);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(result)", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingRuntimeSetupController.PrepareAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("using var selfTrainingSetup", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingSessionController.Create(", setupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingSessionController.Create(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsAiSettingsProvider()", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingCenterSettingsStore.LoadAsync()", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OllamaClient(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new EnhancedVisionAnalysisService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new TechniqueAssessmentService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelfTrainingComparisonService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new PdfProtocolExtractor(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelfTrainingOrchestrator(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new KnowledgeBaseContext()", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.DoesNotContain("SelfTrainingRunExecutionController.RunAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingSetup.Session.Orchestrator.RunAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistorySnapshotBuilder.Build(result, DateTime.UtcNow)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingHistoryStore.AppendRunAsync(snapshot)", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("await LoadSamplesInternalAsync();", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("await RefreshKbStatusAsync();", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingPostRunRefreshController.RefreshAsync(", selfTrainingSource, StringComparison.Ordinal);
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
        var lastMatchSource = ExtractMethodBody(source, "private async Task LoadLastMatchRateAsync()");

        Assert.Contains("SelfTrainingLastMatchRatePresentationBuilder.Build(runs)", lastMatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runs[^1]", lastMatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExactPercent = last.ExactPercent", lastMatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PartialPercent = last.PartialPercent", lastMatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MismatchPercent = last.MismatchPercent", lastMatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NoFindingsPercent = last.NoFindingsPercent", lastMatchSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.False(File.Exists(controllerPath), "Triviale Self-Training-Exception-UI soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunExceptionController.ApplyCanceled(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingRunExceptionController.ApplyFailure(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Selbsttraining abgebrochen.\");", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining abgebrochen.\";", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("Log($\"FEHLER: {ex.GetType().Name}: {ex.Message}\");", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Fehler: {ex.Message}\";", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("selfTrainingUi.SetBusy(false);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("selfTrainingUi.SetSelfTrainingRunning(false);", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator = null;", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingRunFinalizerController.Apply(", selfTrainingSource, StringComparison.Ordinal);
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
        var stopSource = ExtractMethodBody(source, "private void StopSelfTraining()");
        var pauseSource = ExtractMethodBody(source, "private void PauseSelfTraining()");

        Assert.False(File.Exists(controllerPath), "Triviale Self-Training-Run-Control soll inline in der VM stehen.");
        Assert.DoesNotContain("SelfTrainingRunControlController", stopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingRunControlController", pauseSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts?.Cancel();", stopSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining wird abgebrochen...\";", stopSource, StringComparison.Ordinal);
        Assert.Contains("if (_selfTrainingOrchestrator is null) return;", pauseSource, StringComparison.Ordinal);
        Assert.Contains("if (_selfTrainingOrchestrator.IsPaused)", pauseSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator.Resume();", pauseSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining fortgesetzt.\";", pauseSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Pipeline fortgesetzt.\");", pauseSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingOrchestrator.Pause();", pauseSource, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Selbsttraining pausiert.\";", pauseSource, StringComparison.Ordinal);
        Assert.Contains("Log(\"Pipeline pausiert.\");", pauseSource, StringComparison.Ordinal);
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
        var kbUpdateSource = ExtractMethodBody(source, "private async Task<KbIndexOutcome> IncrementalKbUpdateWithReasonAsync(List<TrainingSample> samples, CancellationToken ct)");

        Assert.Contains("TrainingKbIndexRunner.CreateDefault(", kbUpdateSource, StringComparison.Ordinal);
        Assert.Contains("runner.RunAsync(samples, ct)", kbUpdateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new KnowledgeBaseContext()", kbUpdateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new KnowledgeBaseManager(", kbUpdateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var sample in samples)", kbUpdateSource, StringComparison.Ordinal);
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
        var resolverSource = ExtractMethodBody(source, "private async Task<string?> ResolveSelfTrainingSampleIdAsync(InfraSelfImproving.ReviewQueueItem item)");

        Assert.Contains("SelfTrainingReviewSampleIdResolver.ResolveAsync(", resolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault", resolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Abs", resolverSource, StringComparison.Ordinal);
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
        var reconcileSource = ExtractMethodBody(source, "private async Task ReconcileGoldToKbAsync()");

        Assert.Contains("TrainingGoldKbReconcileWorkflowController.RunAsync(", reconcileSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.LoadAsync", reconcileSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSamplesStore.MergeOrUpdateAsync", reconcileSource, StringComparison.Ordinal);
        Assert.Contains("IncrementalKbUpdateWithReasonAsync", reconcileSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KbReconcilePlanner.SelectPending", reconcileSource, StringComparison.Ordinal);
        Assert.DoesNotContain("const int batchSize", reconcileSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var s in batch)", reconcileSource, StringComparison.Ordinal);
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
        var approveSource = ExtractMethodBody(source, "public async Task ApproveReviewItemAsync(");
        var rejectSource = ExtractMethodBody(source, "public async Task RejectReviewItemAsync(");

        Assert.Contains("TrainingReviewQueueCompletionController.ApplyApproved(", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingReviewQueueCompletionController.ApplyRejected(", rejectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("queueService.Remove(item.Id);", approveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("queueService.Remove(item.Id);", rejectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueue.Remove(item);", approveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueue.Remove(item);", rejectSource, StringComparison.Ordinal);
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
        var loadSource = ExtractMethodBody(source, "public void LoadReviewQueue(InfraSelfImproving.ReviewQueueService queueService)");

        Assert.False(File.Exists(controllerPath), "Trivialer Review-Queue-Load soll inline in der VM stehen.");
        Assert.DoesNotContain("TrainingReviewQueueLoadController", loadSource, StringComparison.Ordinal);
        Assert.Contains("ArgumentNullException.ThrowIfNull(queueService);", loadSource, StringComparison.Ordinal);
        Assert.Contains("var items = queueService.GetAll();", loadSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueue.Clear();", loadSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in items)", loadSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueue.Add(item);", loadSource, StringComparison.Ordinal);
        Assert.Contains("ReviewQueueCount = items.Count;", loadSource, StringComparison.Ordinal);
        Assert.Contains("ReviewStatusText = $\"{ReviewQueueCount} Einträge zur Prüfung\";", loadSource, StringComparison.Ordinal);
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
        var kbCheckSource = ExtractMethodBody(source, "private async Task CheckKnowledgeBaseAsync()");

        Assert.Contains("TrainingKnowledgeBaseCheckRunController.TryStart(IsBusy)", kbCheckSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary)", kbCheckSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplySuccess(", kbCheckSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseCheckRunController.ApplyFailure(", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusText = \"Pr", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusText = $\"KB-Pr", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Log($\"KB-Pr", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("summary.LatestVersionAtUtc.Value.ToLocalTime()", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("summary.TopCodes.Count > 0", kbCheckSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KB-Stand: Samples=", kbCheckSource, StringComparison.Ordinal);
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
        var statusSource = ExtractMethodBody(source, "private async Task RefreshKbStatusAsync()");
        var qualitySource = ExtractMethodBody(source, "private async Task RefreshKbQualityAsync()");

        Assert.Contains("TrainingKnowledgeBaseStatusPresentationBuilder.Build(status)", statusSource, StringComparison.Ordinal);
        Assert.Contains("TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs)", qualitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("status.SampleCount switch", statusSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Media.Color.FromRgb", statusSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TakeLast(5)", qualitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("quality.StaleSampleCount > 0", qualitySource, StringComparison.Ordinal);
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
        var persistSource = ExtractMethodBody(source, "private async Task PersistSamplesAsync(TrainingSample? changedSample = null)");

        Assert.Contains("TrainingSamplePersistenceWorkflowController.PersistAsync(", persistSource, StringComparison.Ordinal);
        Assert.DoesNotContain("changedSample.KbIndexState = KbIndexState.Pending", persistSource, StringComparison.Ordinal);
        Assert.DoesNotContain("outcome.IndexedIds.Contains", persistSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample>", persistSource, StringComparison.Ordinal);
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
        var exportSource = ExtractMethodBody(source, "private async Task ExportApprovedAsync()");

        Assert.Contains("TrainingApprovedProtocolExportController.RunAsync(", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuswertungPro.Next.Domain.Protocol.ProtocolEntry", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("approved.Select(s => s.Code)", exportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("s.ExportedUtc = DateTime.UtcNow", exportSource, StringComparison.Ordinal);
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
        var approveSource = ExtractMethodBody(source, "private async Task ApproveSampleAsync()");
        var rejectSource = ExtractMethodBody(source, "private async Task RejectSampleAsync()");
        var removeSource = ExtractMethodBody(source, "private async Task RemoveSampleAsync()");

        Assert.Contains("TrainingSampleDecisionController.Approve(", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Reject(", rejectSource, StringComparison.Ordinal);
        Assert.Contains("TrainingSampleDecisionController.Remove(", removeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Approved", approveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Rejected", rejectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.Status = TrainingSampleStatus.Removed", removeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.KbIndexState = KbIndexState.None", rejectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedSample.KbIndexState = KbIndexState.None", removeSource, StringComparison.Ordinal);
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
        var approveSource = ExtractMethodBody(source, "private void Approve()");
        var rejectSource = ExtractMethodBody(source, "private void Reject()");
        var setNewSource = ExtractMethodBody(source, "private void SetNew()");

        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.Approve)", approveSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.Reject)", rejectSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCaseDecisionController.Apply(SelectedCase, TrainingCaseDecision.SetNew)", setNewSource, StringComparison.Ordinal);
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
        var stepSource = ExtractMethodBody(source, "public void OnSelfTrainingStep(SelfTrainingStep step)");

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
        var stepSource = ExtractMethodBody(source, "public void OnSelfTrainingStep(SelfTrainingStep step)");
        var refreshSource = ExtractMethodBody(source, "private void RefreshMatchRatePercents()");
        var resetSource = ExtractMethodBody(source, "private void ResetSelfTrainingVisuals(bool resetMatchRate = false)");

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
        var resetSource = ExtractMethodBody(source, "private void ResetSelfTrainingVisuals(bool resetMatchRate = false)");

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
        var distributionSource = ExtractMethodBody(source, "private void UpdateCodeDistribution(string code, MatchLevel level)");

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
        var loadSource = ExtractMethodBody(source, "private async Task LoadSamplesInternalAsync()");
        var generateSource = ExtractMethodBody(source, "private async Task GenerateSamplesAsync()");

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
        var loadSource = ExtractMethodBody(source, "public async Task LoadAsync()");

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
        var loadSource = ExtractMethodBody(source, "public async Task LoadAsync()");
        var clearSource = ExtractMethodBody(source, "private void ClearRootFolders()");
        var distributeSource = ExtractMethodBody(source, "private async Task DistributeHaltungAsync()");

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
        var scanSource = ExtractMethodBody(source, "private async Task ScanAsync()");

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
        var selfTrainingLogSource = ExtractMethodBody(source, "private void AddSelfTrainingLog(string message)");
        var logSource = ExtractMethodBody(source, "private void Log(string message)");
        var appendSource = ExtractMethodBody(source, "private void AppendSelfTrainingLogEntry(string entryText)");

        Assert.False(File.Exists(controllerPath), controllerPath);
        Assert.DoesNotContain("TrainingCenterLogController", source, StringComparison.Ordinal);
        Assert.Contains("var entryText = $\"[{DateTime.Now:HH:mm:ss}] {message}\";", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.Contains("AppendSelfTrainingLogEntry(entryText);", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.Contains("var entryText = $\"[{DateTime.Now:HH:mm:ss}] {message}\";", logSource, StringComparison.Ordinal);
        Assert.Contains("LogText += entryText + \"\\n\";", logSource, StringComparison.Ordinal);
        Assert.Contains("AppendSelfTrainingLogEntry(entryText);", logSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLogEntries.Count > 100", appendSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingLogEntries.RemoveAt(0)", appendSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveAt(0)", selfTrainingLogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveAt(0)", logSource, StringComparison.Ordinal);
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
        var startdataSource = ExtractMethodBody(source, "private async Task SuggestProtocolStartdataAsync()");

        Assert.Contains("TrainingProtocolStartdataQueueController.Run(", startdataSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProtocolReviewCandidateFilter.SelectCandidates", startdataSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.GetAll()", startdataSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.EnqueueFromSelfTraining", startdataSource, StringComparison.Ordinal);
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
        var approvalSource = ExtractMethodBody(source, "public async Task ApproveAllStartdataAsync(CancellationToken ct = default)");

        Assert.Contains("TrainingProtocolStartdataApprovalController.ApproveAllAsync(", approvalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var item in items)", approvalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex)", approvalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ok++", approvalSource, StringComparison.Ordinal);
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
        var itemsSource = ExtractMethodBody(source, "private List<InfraSelfImproving.ReviewQueueItem> GetProtocolStartdataReviewItems()");

        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.Count(ReviewQueue)", countSource, StringComparison.Ordinal);
        Assert.Contains("TrainingProtocolStartdataReviewItemSelector.Select(ReviewQueue)", itemsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingMatchLevel", countSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingMatchLevel", itemsSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(allSamples, result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)", selfTrainingSource, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var braceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceIndex..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenrumpf nicht abgeschlossen: {signature}");
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
