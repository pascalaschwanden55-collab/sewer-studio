using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        var service = new TrainingExportService(client, Path.Combine(_root, "missing-eval-set"));
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

    [Fact]
    public async Task ExportAsync_blockiert_bild_aus_verschachteltem_eval_set()
    {
        var evalRoot = Path.Combine(_root, "eval");
        var evalV2Images = Path.Combine(evalRoot, "v2", "images");
        Directory.CreateDirectory(evalV2Images);
        var evalFrame = Path.Combine(evalV2Images, "eval.jpg");
        await File.WriteAllBytesAsync(evalFrame, [4, 5, 6]);
        var evalHash = EvalContaminationGuard.ComputeFileHash(evalFrame)!;
        await File.WriteAllTextAsync(
            Path.Combine(evalRoot, "v2", "_manifest.json"),
            JsonSerializer.Serialize(new
            {
                hashes = new Dictionary<string, object>
                {
                    ["images/eval.jpg"] = new { sha256 = evalHash }
                }
            }));

        var client = new FakeVisionPipelineClient();
        var service = new TrainingExportService(client, evalRoot);
        var result = await service.ExportAsync(
            [new GroundTruthEntry
            {
                MeterStart = 1,
                MeterEnd = 1,
                VsaCode = "BAB",
                Text = "Riss",
                ExtractedFramePath = evalFrame
            }],
            Path.Combine(_root, "output"));

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.SkippedEvalSamples);
        Assert.Contains("Eval-Set", result.Error);
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
