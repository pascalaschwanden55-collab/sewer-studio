using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewer-training-export-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExportAsync_mehr_als_500_samples_sendet_nicht_an_sidecar()
    {
        Directory.CreateDirectory(_root);
        var frame = Path.Combine(_root, "frame.jpg");
        await File.WriteAllBytesAsync(frame, [1, 2, 3]);
        var client = new FakeVisionPipelineClient();
        var service = new TrainingExportService(client);
        var samples = Enumerable.Range(0, 501)
            .Select(i => new GroundTruthEntry
            {
                MeterStart = i,
                MeterEnd = i,
                VsaCode = "BAB",
                Text = "Schaden",
                ExtractedFramePath = frame
            })
            .ToList();

        var result = await service.ExportAsync(samples, "out");

        Assert.False(result.IsSuccess);
        Assert.Contains("maximal 500", result.Error);
        Assert.Equal(0, client.ExportCalls);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeVisionPipelineClient : IVisionPipelineClient
    {
        public int ExportCalls { get; private set; }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
        {
            ExportCalls++;
            return Task.FromResult(new TrainingExportResponseDto(0, 0, 0, [], ""));
        }
    }
}
