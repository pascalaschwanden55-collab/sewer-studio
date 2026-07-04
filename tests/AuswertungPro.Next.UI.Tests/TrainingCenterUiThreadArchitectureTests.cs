using System.IO;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterUiThreadArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_LoadReviewQueue_uses_injected_ui_thread()
    {
        using var temp = new TempDir();
        var uiThread = new RecordingUiThread();
        var vm = CreateViewModel(temp, uiThread);
        var queueService = new InfraSelfImproving.ReviewQueueService();
        queueService.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: "BAA",
            suggestedCode: "BAA",
            meter: 12.3,
            framePath: "frame.jpg",
            matchLevel: "PartialMatch");

        vm.LoadReviewQueue(queueService);

        Assert.Equal(1, uiThread.RunCount);
        Assert.Equal(1, vm.ReviewQueueCount);
        Assert.Single(vm.ReviewQueue);
        Assert.Equal("1 Einträge zur Prüfung", vm.ReviewStatusText);
    }

    private static TrainingCenterViewModel CreateViewModel(TempDir temp, IUiThread uiThread)
        => new(
            new TrainingCenterStore(Path.Combine(temp.Path, "training_center.json")),
            new TrainingCenterImportService(),
            codeCatalog: null,
            kbDiagnostics: new NoopKnowledgeBaseDiagnosticsRunner(),
            settings: null,
            uiThread: uiThread);

    private sealed class RecordingUiThread : IUiThread
    {
        public int RunCount { get; private set; }

        public void Run(Action action)
        {
            RunCount++;
            action();
        }
    }

    private sealed class NoopKnowledgeBaseDiagnosticsRunner : IKnowledgeBaseDiagnosticsRunner
    {
        public Task<KnowledgeBaseStatusReport> ReadStatusAsync(int topCodes = 20, CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseStatusReport(0, 0, 0, 0, 0, null, []));

        public Task<KnowledgeBaseQualityReport> ReadQualityAsync(CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseQualityReport("", 0, "", 0));

        public Task<KnowledgeBaseDiagnosticsSummary> ReadSummaryAsync(int topCodes = 12, CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseDiagnosticsSummary(0, 0, 0, null, 0, "", []));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "training-center-ui-thread-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
