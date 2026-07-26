using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PipelineHealthMonitorQualificationTests
{
    [Fact]
    public async Task Unqualifizierter_Detektor_bleibt_MultiModel_mit_Dino_und_Sam()
    {
        var health = new SidecarHealthResponse(
            Status: "degraded",
            Version: "test",
            Gpu: new GpuStatus(
                CurrentModel: "sam",
                VramAllocatedGb: 1,
                VramTotalGb: 24,
                LoadedModels: new Dictionary<string, GpuLoadedModel>
                {
                    ["yolo"] = new(),
                    ["dino"] = new(),
                    ["sam"] = new(),
                }),
            ModelsPresent: new SidecarModelsPresent(Dino: true, Sam: true),
            DetectorQualification: new SidecarDetectorQualification(
                Qualified: false,
                Reason: "BBox-Kollaps"));
        var monitor = new PipelineHealthMonitor(
            new HealthClient(health),
            aiEnabled: () => true,
            qwenAvailable: () => true);

        var status = await monitor.RefreshOnceAsync();

        Assert.Equal(PipelineHealthLevel.Degraded, status.Level);
        Assert.True(status.MultiModelActive);
        Assert.Equal(false, status.DetectorQualified);
        Assert.True(status.DinoLoaded);
        Assert.True(status.SamLoaded);
        Assert.Contains("BBox-Kollaps", status.Detail);
    }

    [Fact]
    public async Task Fehlende_Qualifikation_bleibt_MultiModel_aber_nicht_gruen()
    {
        var health = new SidecarHealthResponse(
            Status: "ok",
            Version: "test",
            Gpu: new GpuStatus(
                CurrentModel: "sam",
                VramAllocatedGb: 1,
                VramTotalGb: 24,
                LoadedModels: new Dictionary<string, GpuLoadedModel>
                {
                    ["dino"] = new(),
                    ["sam"] = new(),
                }),
            ModelsPresent: new SidecarModelsPresent(Dino: true, Sam: true),
            DetectorQualification: null);
        var monitor = new PipelineHealthMonitor(
            new HealthClient(health),
            aiEnabled: () => true,
            qwenAvailable: () => true);

        var status = await monitor.RefreshOnceAsync();

        Assert.Equal(PipelineHealthLevel.Degraded, status.Level);
        Assert.True(status.MultiModelActive);
        Assert.Null(status.DetectorQualified);
        Assert.Contains("fehlt", status.Detail);
    }

    private sealed class HealthClient(SidecarHealthResponse health) : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(health);

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(
                IsReachable: true,
                IsAuthorized: true,
                StatusCode: 200,
                Health: health,
                Error: null));

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => Task.FromException<YoloResponse>(new NotSupportedException());

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => Task.FromException<DinoResponse>(new NotSupportedException());

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => Task.FromException<SamResponse>(new NotSupportedException());

        public Task<YoloClassifyResponse> ClassifyYoloAsync(
            YoloClassifyRequest request,
            CancellationToken ct = default)
            => Task.FromException<YoloClassifyResponse>(new NotSupportedException());
    }
}
