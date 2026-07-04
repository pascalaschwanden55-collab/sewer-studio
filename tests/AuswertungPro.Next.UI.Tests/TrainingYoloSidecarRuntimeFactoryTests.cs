using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloSidecarRuntimeFactoryTests
{
    [Fact]
    public void Create_erzeugt_runtime_aus_pipeline_config_und_client_factory()
    {
        var config = new PipelineConfig(
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://localhost:8100"),
            SidecarToken: "token",
            Mode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.3,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 30,
            PipeDiameterMmOverride: null);
        PipelineConfig? seenConfig = null;
        var client = new FakeVisionPipelineClient();

        var runtime = TrainingYoloSidecarRuntimeFactory.Create(
            loadPipelineConfig: () => config,
            createClient: cfg =>
            {
                seenConfig = cfg;
                return client;
            });

        Assert.Same(config, runtime.PipelineConfig);
        Assert.Same(client, runtime.Client);
        Assert.Same(config, seenConfig);
    }

    private sealed class FakeVisionPipelineClient : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(null);

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
            => throw new NotSupportedException();
    }
}
