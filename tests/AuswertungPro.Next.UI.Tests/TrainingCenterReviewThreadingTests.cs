using System.IO;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewThreadingTests
{
    [Fact]
    public async Task LoadSamplesCommand_replaces_samples_over_injected_ui_thread()
    {
        using var temp = new TempKnowledgeRoot();
        var uiThread = new RecordingUiThread();
        var sampleStore = new TrainingSampleFileStore(Path.Combine(temp.Path, "training_samples.json"));
        var vm = CreateViewModel(temp, uiThread, sampleStore);
        await sampleStore.SaveAsync(
        [
            new TrainingSample { SampleId = "sample-1", CaseId = "case-1", Code = "BAA" }
        ]);

        await vm.LoadSamplesCommand.ExecuteAsync(null);

        Assert.Equal(1, uiThread.RunCount);
        var sample = Assert.Single(vm.Samples);
        Assert.Equal("sample-1", sample.SampleId);
        Assert.Equal("BAA", sample.Code);
    }

    [Fact]
    public void StartdataCandidateCount_counts_protocol_startdata_items()
    {
        using var temp = new TempKnowledgeRoot();
        var vm = CreateViewModel(temp, new RecordingUiThread());
        vm.ReviewQueue.Add(Item("one", "ProtocolStartdata"));
        vm.ReviewQueue.Add(Item("two", "PartialMatch"));
        vm.ReviewQueue.Add(Item("three", "protocolstartdata"));

        Assert.Equal(2, vm.StartdataCandidateCount);
    }

    private static TrainingCenterViewModel CreateViewModel(
        TempKnowledgeRoot temp,
        IUiThread uiThread,
        ITrainingSampleStore? trainingSamples = null)
        => new(
            new TrainingCenterStore(Path.Combine(temp.Path, "training_center.json")),
            new TrainingCenterImportService(),
            codeCatalog: null,
            kbDiagnostics: new NoopKnowledgeBaseDiagnosticsRunner(),
            settings: null,
            uiThread: uiThread,
            knowledgeBackup: new KnowledgeBackupTransferService(),
            trainingSamples: trainingSamples ?? new TrainingSampleFileStore(
                Path.Combine(temp.Path, "training_samples.json")));

    private static InfraSelfImproving.ReviewQueueItem Item(string id, string? matchLevel)
        => new(id, Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = $"case-{id}",
            SelfTrainingVsaCode = "BAB",
            SelfTrainingSuggestedCode = "BAB",
            SelfTrainingMeter = 1.5,
            SelfTrainingMatchLevel = matchLevel
        };

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

    private sealed class TempKnowledgeRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "training-center-review-threading-" + Guid.NewGuid().ToString("N"));

        public TempKnowledgeRoot()
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
