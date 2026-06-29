using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSelfTrainingArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_run_preparation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingRunPreparationController.PrepareCancellation(_selfTrainingCts)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("_selfTrainingCts = runPreparation.CancellationTokenSource;", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingCts?.Cancel();", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingCts?.Dispose();", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingCts = new CancellationTokenSource();", selfTrainingSource, StringComparison.Ordinal);
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

        Assert.Contains("SelfTrainingAutoScanController.ShouldScan(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingAutoScanController.ScanAsync(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingAutoScanController.StatusText", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (Cases.Count == 0 && _rootFolders.Count > 0)", selfTrainingSource, StringComparison.Ordinal);
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

        Assert.Contains("SelfTrainingReviewQueueController.EnqueueCandidates(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewQueueServiceRef.EnqueueFromSelfTraining(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelfTrainingReviewCandidateSelector.SelectForRun(allSamplesForReview, result)", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_kb_update_statuslogik_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingKbUpdateController.ShouldRun(result)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingKbUpdateController.SelectApprovedSamplesForRun(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingKbUpdateController.MarkPendingBeforeIndex(newApproved)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingKbUpdateController.ApplyOutcome(newApproved, stOutcome)", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("s.KbIndexState is KbIndexState.None or KbIndexState.Error", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("stOutcome.IndexedIds.ToHashSet()", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_startanzeige_an_presenter()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildStart(selectedCase)", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog()", selfTrainingSource, StringComparison.Ordinal);
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
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_service_erzeugung_an_session_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var selfTrainingSource = ExtractMethodBody(source, "private async Task RunSelfTrainingAsync()");

        Assert.Contains("SelfTrainingSessionController.Create(", selfTrainingSource, StringComparison.Ordinal);
        Assert.Contains("using var selfTrainingSession", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OllamaClient(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new EnhancedVisionAnalysisService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new TechniqueAssessmentService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelfTrainingComparisonService(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new PdfProtocolExtractor(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelfTrainingOrchestrator(", selfTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new KnowledgeBaseContext()", selfTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_self_training_run_control_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var stopSource = ExtractMethodBody(source, "private void StopSelfTraining()");
        var pauseSource = ExtractMethodBody(source, "private void PauseSelfTraining()");

        Assert.Contains("SelfTrainingRunControlController.RequestCancel(_selfTrainingCts)", stopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingCts?.Cancel();", stopSource, StringComparison.Ordinal);
        Assert.Contains("SelfTrainingRunControlController.TogglePause(_selfTrainingOrchestrator)", pauseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingOrchestrator.Pause();", pauseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selfTrainingOrchestrator.Resume();", pauseSource, StringComparison.Ordinal);
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
}
