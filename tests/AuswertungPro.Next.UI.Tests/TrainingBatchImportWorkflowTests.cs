using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportWorkflowTests
{
    [Fact]
    public async Task RunAsync_stoppt_nach_scan_stop_und_finalisiert_busy()
    {
        var calls = new List<string>();
        var request = CreateRequest(calls) with
        {
            RootFolders = new[] { "root-a" },
            ScanFolderAsync = _ => Task.FromResult<IReadOnlyList<TrainingCase>>(Array.Empty<TrainingCase>())
        };

        await TrainingBatchImportWorkflow.RunAsync(request);

        Assert.Equal(
            new[]
            {
                "activity-start",
                "busy:True",
                "log-text:",
                "progress-value:0",
                "progress-max:1",
                "clear-preview",
                "reset-visuals",
                "log:Scanne 1 Ordner...",
                "status:Scanne Ordner...",
                "log:  WARNUNG: Ordner existiert nicht: root-a",
                "log:Gefunden: 0 Ordner, 0 mit Protokoll",
                "status:Gefunden: 0 Ordner, 0 mit Protokoll",
                "log:STOP: Keine Ordner mit Protokoll-Dateien gefunden.",
                "status:Keine Ordner mit Protokoll-Dateien gefunden.",
                "busy:False",
                "activity-dispose"
            },
            calls);
    }

    [Fact]
    public async Task RunAsync_loggt_fatalen_fehler_und_finalisiert_busy()
    {
        var calls = new List<string>();
        var request = CreateRequest(calls) with
        {
            RootFolders = new[] { "root-a" },
            DirectoryExists = _ => throw new InvalidOperationException("kaputt")
        };

        await TrainingBatchImportWorkflow.RunAsync(request);

        Assert.Contains("log:FATALER FEHLER: kaputt", calls);
        Assert.Contains("status:Fehler beim Batch-Import: kaputt", calls);
        Assert.Equal("busy:False", calls[^2]);
        Assert.Equal("activity-dispose", calls[^1]);
    }

    private static TrainingBatchImportWorkflowRequest CreateRequest(List<string> calls)
    {
        var batchUi = new TrainingBatchUiSink(
            value => calls.Add($"busy:{value}"),
            value => calls.Add($"log-text:{value}"),
            value => calls.Add($"progress-value:{value}"),
            value => calls.Add($"progress-max:{value}"),
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));
        var caseUi = new TrainingBatchImportCaseUiSink(
            preview => calls.Add($"preview:{preview.CaseInfo}"),
            action =>
            {
                calls.Add("on-ui");
                action();
            },
            _ => calls.Add("add-result"),
            (code, level) => calls.Add($"distribution:{code}:{level}"),
            value => calls.Add($"kb-count:{value}"),
            value => calls.Add($"kb-codes:{value}"),
            value => calls.Add($"case-log:{value}"));

        return new TrainingBatchImportWorkflowRequest(
            RootFolders: Array.Empty<string>(),
            DirectoryExists: _ => false,
            ScanFolderAsync: _ => Task.FromResult<IReadOnlyList<TrainingCase>>(Array.Empty<TrainingCase>()),
            Cases: new List<TrainingCase>(),
            CodeCatalog: null,
            LoadRuntimeSettings: () => new AiRuntimeSettings(
                Enabled: true,
                OllamaBaseUri: new Uri("http://localhost:11434"),
                VisionModel: "vision",
                TextModel: "text",
                EmbedModel: "embed",
                FfmpegPath: "ffmpeg",
                OllamaRequestTimeout: TimeSpan.FromMinutes(2),
                OllamaKeepAlive: "5m",
                OllamaNumCtx: 2048),
            LoadSettingsAsync: () => Task.FromResult(new TrainingCenterSettings()),
            LoadSamplesAsync: () => Task.FromResult(new List<AuswertungPro.Next.Application.Ai.Training.TrainingSample>()),
            MergeAndSaveSamplesAsync: _ => Task.CompletedTask,
            SaveStateAsync: () => Task.CompletedTask,
            ExtractPreviewFrameAsync: (_, _, _) => Task.FromResult<string?>(null),
            GetSelfTrainingResultCount: () => 0,
            BatchUi: batchUi,
            CaseUi: caseUi,
            ReplaceSamples: _ => calls.Add("replace-samples"),
            RefreshKbStatusAsync: () =>
            {
                calls.Add("refresh-kb");
                return Task.CompletedTask;
            },
            ClearLivePreview: () => calls.Add("clear-preview"),
            ResetSelfTrainingVisuals: () => calls.Add("reset-visuals"),
            BeginActivity: () =>
            {
                calls.Add("activity-start");
                return new TrackingDisposable(calls);
            },
            CancellationToken: CancellationToken.None);
    }

    private sealed class TrackingDisposable(List<string> calls) : IDisposable
    {
        public void Dispose() => calls.Add("activity-dispose");
    }
}
