using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloExportWorkflowTests
{
    [Fact]
    public async Task RunAsync_nutzt_lokalen_export_wenn_sidecar_nicht_erreichbar_ist()
    {
        var state = new WorkflowState();
        var root = Path.Combine(Path.GetTempPath(), "sewer-yolo-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var frame = Path.Combine(root, "frame.jpg");
        await File.WriteAllBytesAsync(frame, [1, 2, 3]);
        var approved = ApprovedSample(frame);
        var localCalls = 0;

        try
        {
            await TrainingYoloExportWorkflow.RunAsync(CreateRequest(
                state,
                samples: [approved],
                client: new FakeVisionPipelineClient { Health = null },
                runLocalExportAsync: request =>
                {
                    localCalls++;
                    Assert.Equal([approved], request.ApprovedSamples);
                    Assert.Equal(@"D:\Yolo", request.OutputDir);
                    return Task.CompletedTask;
                }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Assert.Equal(1, localCalls);
        Assert.Equal([true, false], state.BusyStates);
        Assert.Contains("Sidecar nicht erreichbar", state.Logs[1]);
        Assert.Contains("YOLO-Export: 1 Samples", state.Logs[0]);
    }

    [Fact]
    public async Task RunAsync_sendet_payload_an_sidecar_und_fuehrt_completion_aus()
    {
        var state = new WorkflowState();
        var root = Path.Combine(Path.GetTempPath(), "sewer-yolo-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var frame = Path.Combine(root, "frame.jpg");
        await File.WriteAllBytesAsync(frame, [1, 2, 3]);
        var approved = ApprovedSample(frame);
        var exported = 0;
        var completed = 0;
        var response = new TrainingExportResponseDto(
            TotalSamples: 1,
            TrainCount: 1,
            ValCount: 0,
            ClassesUsed: ["BAB"],
            DataYamlPath: @"D:\Yolo\data.yaml");
        var client = new FakeVisionPipelineClient
        {
            Health = new SidecarHealthResponse(
                Status: "ok",
                Version: "1.0",
                Gpu: new GpuStatus("yolo", 1, 8)),
            ExportResponse = response
        };

        try
        {
            await TrainingYoloExportWorkflow.RunAsync(CreateRequest(
                state,
                samples: [approved],
                client: client,
                buildPayloadAsync: request =>
                {
                    Assert.Equal([approved], request.ApprovedSamples);
                    return Task.FromResult(new TrainingYoloSidecarExportPayloadResult(
                        new TrainingExportRequestDto(
                            [new TrainingExportSample("ZmFrZQ==", [new TrainingExportSampleLabel("BAB", 0.1, 0.2, 0.3, 0.4)])],
                            @"D:\Yolo",
                            0.8),
                        SkipEvalHash: 1,
                        SkipEvalCase: 2,
                        SkipNoBox: 3));
                },
                runCompletionAsync: request =>
                {
                    completed++;
                    Assert.Equal(response, request.Response);
                    Assert.Equal([approved], request.ApprovedSamples);
                    return Task.CompletedTask;
                }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        exported++;

        Assert.Equal(1, client.ExportCalls);
        Assert.Equal(1, completed);
        Assert.Equal(1, exported);
        Assert.Equal("YOLO-Export: Sende 1 Samples an Sidecar...", state.Statuses[^1]);
        Assert.Contains(
            state.Logs,
            line => line.Contains("uebersprungen: 1 Eval-Hash, 2 Eval-Haltung, 3 ohne echte Box", StringComparison.Ordinal));
        Assert.Equal([true, false], state.BusyStates);
    }

    [Fact]
    public async Task RunAsync_beendet_frueh_wenn_keine_approved_samples_vorhanden_sind()
    {
        var state = new WorkflowState();
        var folderSelected = false;

        await TrainingYoloExportWorkflow.RunAsync(CreateRequest(
            state,
            samples: [],
            selectOutputDirectory: () =>
            {
                folderSelected = true;
                return @"D:\Yolo";
            }));

        Assert.False(folderSelected);
        Assert.Empty(state.BusyStates);
        Assert.Equal("Keine Approved-Samples mit gueltigen Frames vorhanden.", state.Statuses.Single());
        Assert.Equal("YOLO-Export: Keine exportierbaren Samples gefunden.", state.Logs.Single());
    }

    private static TrainingYoloExportWorkflowRequest CreateRequest(
        WorkflowState state,
        IReadOnlyList<TrainingSample> samples,
        FakeVisionPipelineClient? client = null,
        Func<string?>? selectOutputDirectory = null,
        Func<TrainingYoloSidecarExportPayloadRequest, Task<TrainingYoloSidecarExportPayloadResult>>? buildPayloadAsync = null,
        Func<TrainingYoloSidecarExportCompletionRequest, Task>? runCompletionAsync = null,
        Func<TrainingYoloLocalExportWorkflowRequest, Task>? runLocalExportAsync = null)
        => new(
            IsBusy: () => false,
            Samples: samples,
            IsTrainingExportEligible: _ => true,
            PersistSamplesAsync: () => Task.CompletedTask,
            SelectOutputDirectory: selectOutputDirectory ?? (() => @"D:\Yolo"),
            ResetCancellation: () => CancellationToken.None,
            CreateSidecarRuntime: () => new TrainingYoloSidecarRuntime(
                PipelineConfig: new PipelineConfig(
                    MultiModelEnabled: true,
                    SidecarUrl: new Uri("http://localhost:8100"),
                    SidecarToken: null,
                    Mode: PipelineMode.Auto,
                    YoloConfidence: 0.25,
                    YoloClassConfidence: new Dictionary<string, double>(),
                    DinoBoxThreshold: 0.25,
                    DinoTextThreshold: 0.25,
                    SidecarTimeoutSec: 30,
                    PipeDiameterMmOverride: null),
                Client: client ?? new FakeVisionPipelineClient()),
            LoadEvalSets: () => new EvalContaminationSets(new HashSet<string>(), new HashSet<string>()),
            BuildSidecarPayloadAsync: buildPayloadAsync ?? (request => Task.FromResult(new TrainingYoloSidecarExportPayloadResult(
                new TrainingExportRequestDto(
                    [new TrainingExportSample("ZmFrZQ==", [new TrainingExportSampleLabel("BAB", 0.1, 0.2, 0.3, 0.4)])],
                    request.OutputDir,
                    request.TrainSplit),
                SkipEvalHash: 0,
                SkipEvalCase: 0,
                SkipNoBox: 0))),
            RunSidecarCompletionAsync: runCompletionAsync ?? (_ => Task.CompletedTask),
            RunLocalExportAsync: runLocalExportAsync ?? (_ => Task.CompletedTask),
            SetBusy: state.BusyStates.Add,
            Log: state.Logs.Add,
            SetProgressMax: value => state.ProgressMax = value,
            SetProgressValue: value => state.ProgressValue = value,
            SetStatusText: state.Statuses.Add);

    private static TrainingSample ApprovedSample(string framePath)
        => new()
        {
            SampleId = "sample-a",
            CaseId = "haltung-a",
            Status = TrainingSampleStatus.Approved,
            FramePath = framePath,
            Code = "BAB",
            BboxXCenter = 0.1,
            BboxYCenter = 0.2,
            BboxWidth = 0.3,
            BboxHeight = 0.4
        };

    private sealed class WorkflowState
    {
        public List<bool> BusyStates { get; } = new();
        public List<string> Logs { get; } = new();
        public List<string> Statuses { get; } = new();
        public int ProgressMax { get; set; }
        public int ProgressValue { get; set; }
    }

    private sealed class FakeVisionPipelineClient : IVisionPipelineClient
    {
        public SidecarHealthResponse? Health { get; set; } = new("ok", "1.0", null);
        public TrainingExportResponseDto ExportResponse { get; set; } = new(1, 1, 0, ["BAB"], @"D:\Yolo\data.yaml");
        public int ExportCalls { get; private set; }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult(Health);

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
        {
            ExportCalls++;
            return Task.FromResult(ExportResponse);
        }
    }
}
