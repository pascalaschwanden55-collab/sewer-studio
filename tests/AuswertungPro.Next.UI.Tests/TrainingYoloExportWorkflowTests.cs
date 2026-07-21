using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloExportWorkflowTests
{
    [Fact]
    public async Task RunAsync_delegiert_genau_einen_Befehl_an_den_Koordinator()
    {
        var plan = Plan(withImage: true);
        var coordinator = new RecordingCoordinator((_, progress, _) =>
        {
            progress?.Report(new TrainingYoloExportProgress(
                TrainingYoloExportProgressStage.ExecutingPlan,
                "Plan wird geschrieben.",
                Total: 1));
            return Task.FromResult(Completed(plan));
        });
        var sample = new TrainingSample { SampleId = "sample-1" };
        var state = new UiState();
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(
            coordinator,
            state,
            [sample],
            utcNow: () => now));

        var command = Assert.IsType<TrainingYoloExportCommand>(coordinator.LastCommand);
        Assert.Same(sample, Assert.Single(command.UpdateTargets!));
        Assert.Equal(now, command.GeneratedUtc);
        Assert.Equal([true, false], state.BusyValues);
        Assert.Equal(1, state.ProgressMax);
        Assert.Equal(1, state.ProgressValue);
        Assert.Contains("YOLO-Export fertig", state.Status, StringComparison.Ordinal);
        Assert.Contains(state.Logs, line => line.Contains("Weg: Sidecar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_macht_nichts_wenn_bereits_ein_Lauf_aktiv_ist()
    {
        var coordinator = new RecordingCoordinator((_, _, _) =>
            throw new InvalidOperationException("darf nicht laufen"));
        var state = new UiState { IsBusy = true };

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(coordinator, state));

        Assert.Null(coordinator.LastCommand);
        Assert.Empty(state.BusyValues);
    }

    [Fact]
    public async Task RunAsync_meldet_einen_leeren_Plan_ohne_Scheinerfolg()
    {
        var coordinator = new RecordingCoordinator((_, _, _) => Task.FromResult(
            new TrainingYoloExportResult(
                TrainingYoloExportResultStatus.NoImages,
                Plan(withImage: false),
                null,
                new TrainingExportCompletionResult(0, []))));
        var state = new UiState();

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(coordinator, state));

        Assert.Contains("keine exportierbaren Bilder", state.Status, StringComparison.Ordinal);
        Assert.Equal([true, false], state.BusyValues);
    }

    [Fact]
    public async Task RunAsync_meldet_Abbruch_und_setzt_Busy_zurueck()
    {
        var coordinator = new RecordingCoordinator((_, _, _) =>
            Task.FromException<TrainingYoloExportResult>(new OperationCanceledException()));
        var state = new UiState();

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(coordinator, state));

        Assert.Equal("YOLO-Export abgebrochen.", state.Status);
        Assert.Equal([true, false], state.BusyValues);
    }

    [Fact]
    public async Task RunAsync_meldet_Fehler_und_setzt_Busy_zurueck()
    {
        var coordinator = new RecordingCoordinator((_, _, _) =>
            Task.FromException<TrainingYoloExportResult>(new InvalidDataException("Register fehlt")));
        var state = new UiState();

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(coordinator, state));

        Assert.Contains("Register fehlt", state.Status, StringComparison.Ordinal);
        Assert.Equal([true, false], state.BusyValues);
    }

    [Fact]
    public void RequestFactory_uebernimmt_den_injizierten_Koordinator_ohne_Fallback()
    {
        var coordinator = new RecordingCoordinator((_, _, _) => Task.FromResult(Completed(Plan(true))));
        var dependencies = new TrainingYoloExportDependencies(coordinator);

        var request = TrainingYoloExportRequestFactory.Create(
            [],
            dependencies,
            () => false,
            () => CancellationToken.None,
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { });

        Assert.Same(coordinator, request.Coordinator);
    }

    private static TrainingYoloExportWorkflowRequest CreateRequest(
        ITrainingYoloExportCoordinator coordinator,
        UiState state,
        IReadOnlyList<TrainingSample>? samples = null,
        Func<DateTimeOffset>? utcNow = null)
        => new(
            IsBusy: () => state.IsBusy,
            Samples: samples ?? [],
            Coordinator: coordinator,
            ResetCancellation: () => CancellationToken.None,
            UtcNow: utcNow ?? (() => DateTimeOffset.UtcNow),
            SetBusy: value =>
            {
                state.IsBusy = value;
                state.BusyValues.Add(value);
            },
            Log: state.Logs.Add,
            SetProgressMax: value => state.ProgressMax = value,
            SetProgressValue: value => state.ProgressValue = value,
            SetStatusText: value => state.Status = value);

    private static TrainingYoloExportResult Completed(TrainingExportPlan plan)
    {
        var result = new TrainingExportExecutionResult(
            plan.PlanId,
            plan.PlanId,
            TrainingExportExecutionStatus.Created,
            plan.Images.Count,
            plan.Images.Count,
            0,
            plan.Classes.Count,
            Path.Combine(@"C:\KI_BRAIN\training\datasets", plan.PlanId),
            Path.Combine(@"C:\KI_BRAIN\training\datasets", plan.PlanId, "data.yaml"),
            Path.Combine(@"C:\KI_BRAIN\training\datasets", plan.PlanId, "manifest.json"),
            plan.Images.Select(image => image.ImageSha256).ToArray());
        return new TrainingYoloExportResult(
            TrainingYoloExportResultStatus.Completed,
            plan,
            new TrainingExportExecutionOutcome(TrainingExportExecutionRoute.Sidecar, result, "1.2.0"),
            new TrainingExportCompletionResult(0, []));
    }

    private static TrainingExportPlan Plan(bool withImage)
    {
        var imageHash = new string('b', 64);
        var images = withImage
            ? new[]
            {
                new TrainingExportPlannedImage(
                    imageHash,
                    "H1",
                    TrainingExportTarget.Train,
                    $"img_{imageHash}.png",
                    [])
            }
            : [];
        return new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('a', 64),
            new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero),
            "inventory-run",
            new Dictionary<string, string>(),
            2,
            new string('c', 64),
            new string('d', 64),
            [],
            ["BAB_riss"],
            withImage ? ["H1"] : [],
            [],
            new Dictionary<string, int> { ["BAB_riss"] = 0 },
            images,
            []);
    }

    private sealed class RecordingCoordinator(
        Func<TrainingYoloExportCommand, IProgress<TrainingYoloExportProgress>?, CancellationToken,
            Task<TrainingYoloExportResult>> run) : ITrainingYoloExportCoordinator
    {
        public TrainingYoloExportCommand? LastCommand { get; private set; }

        public Task<TrainingYoloExportResult> RunAsync(
            TrainingYoloExportCommand command,
            IProgress<TrainingYoloExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return run(command, progress, cancellationToken);
        }
    }

    private sealed class UiState
    {
        public bool IsBusy { get; set; }
        public List<bool> BusyValues { get; } = [];
        public List<string> Logs { get; } = [];
        public int ProgressMax { get; set; }
        public int ProgressValue { get; set; }
        public string Status { get; set; } = "";
    }
}
