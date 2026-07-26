using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiControllerTests
{
    [Fact]
    public void ApplyRuntime_stores_runtime_services_and_begin_analysis_creates_cancellation()
    {
        var controller = new CodingAiController();
        var liveDetection = new LiveDetectionService(
            new OllamaClient(new Uri("http://127.0.0.1:11434")),
            "vision-test:latest");
        var qualityGate = new QualityGateService();
        var verifier = new GuidedVerificationService(
            new OllamaClient(new Uri("http://127.0.0.1:11434")),
            "vision-test:latest");
        var runtime = Runtime(
            enabled: true,
            liveDetection,
            enhancedVision: null,
            qualityGate,
            verifier);

        controller.ApplyRuntime(runtime);

        Assert.Same(liveDetection, controller.LiveDetection);
        Assert.Same(qualityGate, controller.QualityGate);
        Assert.Same(verifier, controller.ProtocolVerifier);
        Assert.Same(runtime.PipelineConfig, controller.PipelineConfig);
        Assert.Equal("vision-test:latest", controller.ModelName);
        Assert.True(controller.QwenAvailable);

        Assert.True(controller.TryBeginAnalysis());
        Assert.True(controller.IsAnalyzing);
        Assert.NotNull(controller.AnalysisCancellation);
        Assert.False(controller.TryBeginAnalysis());

        controller.EndAnalysis();
        controller.DisposeAnalysisCancellation();

        Assert.False(controller.IsAnalyzing);
        Assert.Null(controller.AnalysisCancellation);
    }

    [Fact]
    public void ApplyRuntime_clears_services_for_disabled_runtime_and_prevents_analysis()
    {
        var controller = new CodingAiController();

        controller.ApplyRuntime(Runtime(
            enabled: false,
            liveDetection: null,
            enhancedVision: null,
            qualityGate: null));

        Assert.Null(controller.LiveDetection);
        Assert.Null(controller.EnhancedVision);
        Assert.Null(controller.QualityGate);
        Assert.Null(controller.ProtocolVerifier);
        Assert.False(controller.QwenAvailable);
        Assert.False(controller.TryBeginAnalysis());
        Assert.Null(controller.AnalysisCancellation);
    }

    [Fact]
    public async Task Health_monitor_lifecycle_starts_refreshes_stops_and_unsubscribes()
    {
        var controller = new CodingAiController();
        var monitor = new FakePipelineHealthMonitor();
        var received = 0;

        controller.SetAiEnabled(true);
        controller.StartHealthMonitor(monitor, (_, _) => received++);

        Assert.True(monitor.Started);
        Assert.True(controller.HasHealthMonitor);

        var status = await controller.RefreshHealthOnceAsync();
        monitor.RaiseStatus();

        Assert.Same(monitor.CurrentStatus, status);
        Assert.Equal(1, received);

        var stopTask = controller.StopHealthMonitor();
        Assert.NotNull(stopTask);
        await stopTask;
        monitor.RaiseStatus();

        Assert.True(monitor.Stopped);
        Assert.False(controller.HasHealthMonitor);
        Assert.False(controller.AiEnabled);
        Assert.Equal(1, received);
    }

    [Fact]
    public void Dispose_gibt_VisionClient_der_Runtime_frei()
    {
        var vision = new DisposableFakeVisionClient();
        var controller = new CodingAiController();
        controller.ApplyRuntime(Runtime(
            enabled: true, liveDetection: null, enhancedVision: null, qualityGate: null, visionClient: vision));

        controller.Dispose();

        Assert.True(vision.Disposed);
    }

    [Fact]
    public void ApplyRuntime_gibt_vorherige_Runtime_frei_aber_nicht_die_aktuelle()
    {
        // Jeder Codiermodus-Wiedereintritt baut einen neuen VisionClient. Die vorherige Runtime
        // muss beim naechsten ApplyRuntime freigegeben werden, sonst leaken die HttpClients.
        var oldVision = new DisposableFakeVisionClient();
        var newVision = new DisposableFakeVisionClient();
        var controller = new CodingAiController();

        controller.ApplyRuntime(Runtime(
            enabled: true, liveDetection: null, enhancedVision: null, qualityGate: null, visionClient: oldVision));
        controller.ApplyRuntime(Runtime(
            enabled: true, liveDetection: null, enhancedVision: null, qualityGate: null, visionClient: newVision));

        Assert.True(oldVision.Disposed);    // vorherige Runtime freigegeben
        Assert.False(newVision.Disposed);   // aktuelle bleibt nutzbar

        controller.Dispose();
        Assert.True(newVision.Disposed);
    }

    private static CodingAiRuntime Runtime(
        bool enabled,
        LiveDetectionService? liveDetection,
        EnhancedVisionAnalysisService? enhancedVision,
        QualityGateService? qualityGate,
        GuidedVerificationService? protocolVerifier = null,
        IVisionPipelineClient? visionClient = null)
        => new(
            new AiRuntimeSettings(
                enabled,
                new Uri("http://127.0.0.1:11434"),
                "vision-test:latest",
                "text-test:latest",
                "embed-test:latest",
                FfmpegPath: null,
                TimeSpan.FromSeconds(30),
                "24h",
                4096),
            new PipelineConfig(
                MultiModelEnabled: true,
                new Uri("http://127.0.0.1:8000"),
                SidecarToken: "token",
                PipelineMode.Auto,
                YoloConfidence: 0.35,
                YoloClassConfidence: new Dictionary<string, double>(),
                DinoBoxThreshold: 0.3,
                DinoTextThreshold: 0.25,
                SidecarTimeoutSec: 60,
                PipeDiameterMmOverride: 300),
            "vision-test:latest",
            liveDetection,
            enhancedVision,
            qualityGate,
            protocolVerifier,
            VisionClient: visionClient,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);

    private sealed class FakePipelineHealthMonitor : IPipelineHealthMonitor
    {
        public PipelineHealthStatus CurrentStatus { get; } = new(
            PipelineHealthLevel.Full,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: true,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "ok",
            Detail: "ready");

        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public event EventHandler<PipelineHealthStatus>? StatusChanged;

        public void Start()
            => Started = true;

        public Task StopAsync()
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentStatus);

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public void RaiseStatus()
            => StatusChanged?.Invoke(this, CurrentStatus);
    }

    private sealed class DisposableFakeVisionClient : IVisionPipelineClient, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
