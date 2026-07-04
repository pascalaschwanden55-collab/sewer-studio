using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSampleGenerationWorkflowTests
{
    [Fact]
    public async Task RunAsync_ueberspringt_ohne_auswahl_oder_wenn_busy()
    {
        var calls = new List<string>();
        var request = CreateRequest(
            noSelection: true,
            resetCancellation: () =>
            {
                calls.Add("reset");
                return CancellationToken.None;
            },
            loadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample>());
            });

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.Empty(calls);

        request = CreateRequest(
            getIsBusy: () => true,
            resetCancellation: () =>
            {
                calls.Add("reset");
                return CancellationToken.None;
            });

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_generiert_persistiert_und_haengt_neue_samples_an()
    {
        var state = new WorkflowState();
        var appended = new List<TrainingSample>();
        var saved = new List<TrainingSample>();
        var existing = new List<TrainingSample> { Sample("old", "AAA", "sig-old") };
        var generated = new List<TrainingSample>
        {
            Sample("new-1", "BAA", "sig-new"),
            Sample("new-2", "BAB", "sig-new-2")
        };
        TrainingCaseInput? generatedInput = null;
        IReadOnlyCollection<string>? generatedSignatures = null;
        var activity = new DisposableProbe();

        var request = CreateRequest(
            state: state,
            beginActivity: () => activity,
            loadSamplesAsync: () => Task.FromResult(existing),
            generateWithDiagnosticsAsync: (input, signatures, _) =>
            {
                generatedInput = input;
                generatedSignatures = signatures;
                return Task.FromResult(Result(generated));
            },
            saveSamplesAsync: samples =>
            {
                saved.AddRange(samples);
                return Task.CompletedTask;
            },
            appendSamples: samples => appended.AddRange(samples));

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.NotNull(generatedInput);
        Assert.Equal("101.1-102.1", generatedInput.CaseId);
        Assert.Contains("sig-old", generatedSignatures!);
        Assert.Equal(generated, saved);
        Assert.Equal(generated, appended);
        Assert.True(activity.Disposed);
        Assert.False(state.IsBusy);
        Assert.Equal("2 neue Samples generiert für 101.1-102.1.", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_ohne_neue_samples_setzt_diagnose_status_und_speichert_nicht()
    {
        var state = new WorkflowState();
        var saved = false;
        var appended = false;
        var trainingCase = Case();
        trainingCase.ProtocolPath = @"C:\Training\missing.pdf";
        var request = CreateRequest(
            state: state,
            selectedCase: trainingCase,
            generateWithDiagnosticsAsync: (_, _, _) =>
                Task.FromResult(Result(
                    [],
                    parsedEntries: 0,
                    outcome: TrainingSampleGenerationOutcome.ProtocolFileMissing)),
            saveSamplesAsync: _ =>
            {
                saved = true;
                return Task.CompletedTask;
            },
            appendSamples: _ => appended = true);

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.False(saved);
        Assert.False(appended);
        Assert.False(state.IsBusy);
        Assert.Equal(@"Protokolldatei fehlt: C:\Training\missing.pdf", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_faengt_abbruch_und_fehler_und_setzt_busy_zurueck()
    {
        var state = new WorkflowState();
        var request = CreateRequest(
            state: state,
            generateWithDiagnosticsAsync: (_, _, _) => throw new OperationCanceledException());

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.False(state.IsBusy);
        Assert.Equal("Sample-Generierung abgebrochen.", state.StatusText);

        request = CreateRequest(
            state: state,
            generateWithDiagnosticsAsync: (_, _, _) => throw new InvalidOperationException("kaputt"));

        await TrainingCenterSampleGenerationWorkflow.RunAsync(request);

        Assert.False(state.IsBusy);
        Assert.Equal("Fehler bei Sample-Generierung: kaputt", state.StatusText);
    }

    private static TrainingCenterSampleGenerationWorkflowRequest CreateRequest(
        WorkflowState? state = null,
        TrainingCase? selectedCase = null,
        Func<bool>? getIsBusy = null,
        Action<bool>? setIsBusy = null,
        bool noSelection = false,
        Func<CancellationToken>? resetCancellation = null,
        Func<IDisposable>? beginActivity = null,
        Func<Task<List<TrainingSample>>>? loadSamplesAsync = null,
        Func<TrainingCaseInput, IReadOnlyCollection<string>, CancellationToken, Task<TrainingSampleGenerationResult>>? generateWithDiagnosticsAsync = null,
        Func<List<TrainingSample>, Task>? saveSamplesAsync = null,
        Action<IReadOnlyList<TrainingSample>>? appendSamples = null,
        Action<string>? setStatusText = null)
    {
        state ??= new WorkflowState();
        return new TrainingCenterSampleGenerationWorkflowRequest(
            SelectedCase: noSelection ? null : selectedCase ?? Case(),
            GetIsBusy: getIsBusy ?? (() => state.IsBusy),
            SetIsBusy: setIsBusy ?? (value => state.IsBusy = value),
            ResetCancellation: resetCancellation ?? (() => CancellationToken.None),
            BeginActivity: beginActivity ?? (() => new DisposableProbe()),
            LoadSamplesAsync: loadSamplesAsync ?? (() => Task.FromResult(new List<TrainingSample>())),
            GenerateWithDiagnosticsAsync: generateWithDiagnosticsAsync ?? ((_, _, _) => Task.FromResult(Result([Sample("new", "BAA", "sig-new")]))),
            SaveSamplesAsync: saveSamplesAsync ?? (_ => Task.CompletedTask),
            AppendSamples: appendSamples ?? (_ => { }),
            SetStatusText: setStatusText ?? (value => state.StatusText = value));
    }

    private static TrainingCase Case()
        => new()
        {
            CaseId = "101.1-102.1",
            FolderPath = @"C:\Training\Case",
            VideoPath = @"C:\Training\Case\video.mp4",
            ProtocolPath = @"C:\Training\Case\protocol.pdf",
            InspectionDate = new DateTime(2026, 6, 29)
        };

    private static TrainingSample Sample(string id, string code, string signature)
        => new()
        {
            SampleId = id,
            CaseId = "101.1-102.1",
            Code = code,
            Signature = signature
        };

    private static TrainingSampleGenerationResult Result(
        List<TrainingSample> samples,
        int parsedEntries = 1,
        TrainingSampleGenerationOutcome outcome = TrainingSampleGenerationOutcome.Success)
        => new(samples, parsedEntries, DuplicateSkipped: 0, outcome);

    private sealed class WorkflowState
    {
        public bool IsBusy { get; set; }
        public string StatusText { get; set; } = "";
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
