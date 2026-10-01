using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class VideoAnalysisPipeDiameterTests
{
    [Fact]
    public void QwenKontext_UnbekannterDn_IstKeineErfundeneNullmessung()
    {
        var context = new MultiModelFrameResult(0, 0, true, [], [], 640, 480, 0, 0, 0);

        var prompt = EnhancedVisionPromptBuilder.BuildContextPrompt(context, 0);

        Assert.Contains("Rohrdurchmesser: unbekannt", prompt);
        Assert.DoesNotContain("DN0", prompt);
        Assert.Contains("Millimeter", prompt);
    }

    [Fact]
    public void QwenKontext_OhneKalibrierung_KennzeichnetDieGroessenannahme()
    {
        var context = new MultiModelFrameResult(0, 0, true, [], [], 640, 480, 0, 0, 0);

        var prompt = EnhancedVisionPromptBuilder.BuildContextPrompt(context, 600);

        Assert.Contains("DN600", prompt);
        Assert.Contains("70", prompt);
        Assert.Contains("Schätzung", prompt);
    }

    [Theory]
    [InlineData(300, null, 300, 47)]
    [InlineData(600, null, 600, 94)]
    [InlineData(600, 300, 600, 94)]
    [InlineData(null, 600, 600, 94)]
    [InlineData(null, null, null, null)]
    [InlineData(null, 0, null, null)]
    [InlineData(null, -300, null, null)]
    public async Task Analyse_VerwendetHaltungsDnOhneStille300Annahme(
        int? holdingDiameter, int? globalDiameter, int? expectedDiameter, int? expectedHeight)
    {
        VsaResolverTestCatalog.ConfigureDefault();
        var config = new PipelineConfig(true, new Uri("http://localhost:5001"), null,
            PipelineMode.MultiModel, 0.25, [], 0.25, 0.20, 30, globalDiameter)
        {
            PipeDiameterMm = holdingDiameter
        };
        var client = new DiameterClient();
        var service = new MultiModelAnalysisService(new NullTraceWriter(), client, config, "ffmpeg",
            frameSource: Frames, durationProbe: (_, _) => Task.FromResult(1.0))
        {
            FrameStepSeconds = 1,
            UseClsPrefilter = false,
            ClassifierDecisionEnabled = false,
            ClassifierOnlyStructuralEnabled = false
        };

        var result = await service.AnalyzeAsync("synthetisches-video.mp4");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, client.SamCalls);
        Assert.Equal(expectedDiameter, client.LastDiameter);
        var detection = Assert.Single(result.Detections);
        Assert.Equal(expectedHeight, detection.HeightMm);
        Assert.Equal(expectedHeight, detection.WidthMm);
    }

    private static async IAsyncEnumerable<FrameData> Frames(
        string ffmpeg, string video, double step, double duration,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        yield return new FrameData(0, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVQI12P4z8AAAQIBxPMzAAAAAElFTkSuQmCC"));
        await Task.CompletedTask;
    }

    private sealed class DiameterClient : IVisionPipelineClient
    {
        internal int SamCalls { get; private set; }
        internal int? LastDiameter { get; private set; }
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse("ok", "1.2.0", null,
                DetectorQualification: new SidecarDetectorQualification(true, null)));
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloResponse(true, [], "structural", 1));
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => Task.FromResult(new DinoResponse([new DinoDetectionDto(570, 200, 640, 270, "Riss", 0.95, "Riss")], 1));
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse([], 1));
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            SamCalls++;
            LastDiameter = request.PipeDiameterMm;
            return Task.FromResult(new SamResponse([
                new SamMaskResult("Riss", 0.95, [570, 200, 640, 270], "", 4900, 640 * 480, 70, 70, 605, 235)
            ], 640, 480, 1));
        }
    }

    private sealed class NullTraceWriter : IPipelineTraceWriter
    {
        public Task WriteAsync(PipelineTraceEntry entry) => Task.CompletedTask;
        public Task WriteSummaryAsync(string runId, TelemetrySummary summary) => Task.CompletedTask;
        public string? ResolvePath(string runId) => null;
        public string? ResolveSummaryPath(string runId) => null;
    }
}
