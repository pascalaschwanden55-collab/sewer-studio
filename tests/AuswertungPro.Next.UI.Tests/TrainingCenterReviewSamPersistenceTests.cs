using System.IO;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewSamPersistenceTests
{
    [Fact]
    public void ReviewSamSegmentierung_bleibt_am_Kandidaten_bis_die_Auswahl_wechselt()
    {
        using var temp = new TempDir();
        var vm = CreateViewModel(temp);
        var mask = new TrainingSegmentationMask(
            MaskRle: "1,10,20",
            ImageWidth: 720,
            ImageHeight: 576,
            MaskAreaPixels: 10,
            Confidence: 0.91,
            Label: "BAB");

        vm.SelectedReviewItem = ReviewItem("one");
        vm.PendingSamMask = mask;

        Assert.Same(mask, vm.PendingSamMask);

        vm.SelectedReviewItem = ReviewItem("two");

        Assert.Null(vm.PendingSamMask);
    }

    private static TrainingCenterViewModel CreateViewModel(TempDir temp)
        => new(
            new TrainingCenterStore(Path.Combine(temp.Path, "training_center.json")),
            new TrainingCenterImportService(),
            codeCatalog: null,
            kbDiagnostics: new NoopKnowledgeBaseDiagnosticsRunner(),
            settings: null,
            uiThread: new ImmediateUiThread());

    private static InfraSelfImproving.ReviewQueueItem ReviewItem(string id)
        => new(id, Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = $"case-{id}",
            SelfTrainingVsaCode = "BAB",
            SelfTrainingSuggestedCode = "BAB",
            SelfTrainingMeter = 1.5,
            SelfTrainingMatchLevel = "PartialMatch"
        };

    private sealed class ImmediateUiThread : IUiThread
    {
        public void Run(Action action) => action();
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
            "training-center-review-sam-" + Guid.NewGuid().ToString("N"));

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
