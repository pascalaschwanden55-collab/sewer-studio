using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Infrastructure.Ai.Training.Preview;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Preview;

public sealed class TrainingPreviewDetectionServiceTests
{
    [Fact]
    public async Task Candidate_uses_only_the_dedicated_bcc_endpoint()
    {
        var pipeline = new FakePipelineClient();
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ => [1, 2, 3]);

        var result = await service.DetectAsync(
            @"C:\frames\bogen.jpg",
            TrainingPreviewModelKind.BccTestCandidate);

        Assert.True(result.Available);
        Assert.Equal(1, pipeline.CandidateCalls);
        Assert.Equal(0, pipeline.StandardCalls);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), pipeline.LastRequest?.ImageBase64);
        Assert.Equal("BCC_bogen", Assert.Single(result.Detections).ClassName);
        Assert.Equal("test-sha", result.ModelSha256);
    }

    [Fact]
    public async Task Standard_uses_the_existing_production_endpoint()
    {
        var pipeline = new FakePipelineClient
        {
            Qualification = new SidecarDetectorQualification(true, null)
        };
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ => [4, 5, 6]);

        var result = await service.DetectAsync(
            @"C:\frames\riss.jpg",
            TrainingPreviewModelKind.ActiveStandard);

        Assert.True(result.Available);
        Assert.Equal(0, pipeline.CandidateCalls);
        Assert.Equal(1, pipeline.StandardCalls);
        Assert.Equal("production", result.ModelName);
    }

    [Fact]
    public async Task Standard_without_qualification_is_blocked_before_image_or_model_call()
    {
        var imageReads = 0;
        var pipeline = new FakePipelineClient { Qualification = null };
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ =>
            {
                imageReads++;
                return [4, 5, 6];
            });

        var result = await service.DetectAsync(
            @"C:\frames\riss.jpg",
            TrainingPreviewModelKind.ActiveStandard);

        Assert.False(result.Available);
        Assert.Empty(result.Detections);
        Assert.Equal(0, imageReads);
        Assert.Equal(0, pipeline.StandardCalls);
        Assert.Contains("fehlt", result.Error);
    }

    [Fact]
    public async Task Standard_with_unreadable_qualification_is_blocked()
    {
        var pipeline = new FakePipelineClient { HealthError = new InvalidDataException("kaputt") };
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ => [4, 5, 6]);

        var result = await service.DetectAsync(
            @"C:\frames\riss.jpg",
            TrainingPreviewModelKind.ActiveStandard);

        Assert.False(result.Available);
        Assert.Equal(0, pipeline.StandardCalls);
        Assert.Contains("nicht gelesen", result.Error);
    }

    [Fact]
    public async Task Standard_when_detection_response_is_not_qualified_discards_all_boxes()
    {
        var pipeline = new FakePipelineClient
        {
            Qualification = new SidecarDetectorQualification(true, null),
            ResponseQualified = false,
            ResponseQualificationReason = "Freigabe zwischenzeitlich entzogen"
        };
        var service = new TrainingPreviewDetectionService(pipeline, _ => [4, 5, 6]);

        var result = await service.DetectAsync(
            @"C:\frames\riss.jpg",
            TrainingPreviewModelKind.ActiveStandard);

        Assert.False(result.Available);
        Assert.Empty(result.Detections);
        Assert.Equal(1, pipeline.StandardCalls);
        Assert.Contains("entzogen", result.Error);
    }

    private sealed class FakePipelineClient : IVisionPipelineClient
    {
        private static readonly IReadOnlyList<YoloDetectionDto> Detections =
        [
            new(10, 20, 100, 120, "BCC_bogen", 0.9)
        ];

        public int CandidateCalls { get; private set; }
        public int StandardCalls { get; private set; }
        public YoloRequest? LastRequest { get; private set; }
        public SidecarDetectorQualification? Qualification { get; set; }
        public Exception? HealthError { get; set; }
        public bool? ResponseQualified { get; set; } = true;
        public string? ResponseQualificationReason { get; set; }

        public Task<YoloResponse> DetectYoloAsync(
            YoloRequest request,
            CancellationToken ct = default)
        {
            StandardCalls++;
            LastRequest = request;
            return Task.FromResult(new YoloResponse(
                IsRelevant: true,
                Detections,
                FrameClass: "damage",
                InferenceTimeMs: 9,
                ModelName: "production",
                DetectorQualified: ResponseQualified,
                DetectorQualificationReason: ResponseQualificationReason,
                DetectorArtifactSha256: "production-sha"));
        }

        public Task<BccTestYoloResponse> DetectBccTestYoloAsync(
            YoloRequest request,
            CancellationToken ct = default)
        {
            CandidateCalls++;
            LastRequest = request;
            return Task.FromResult(new BccTestYoloResponse(
                Available: true,
                Error: null,
                IsRelevant: true,
                Detections,
                FrameClass: "damage",
                InferenceTimeMs: 8,
                CandidateId: "bcc_candidate",
                CandidateSha256: "test-sha",
                ModelName: "bcc_test",
                Device: "cpu"));
        }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => HealthError is not null
                ? Task.FromException<SidecarHealthResponse?>(HealthError)
                : Task.FromResult<SidecarHealthResponse?>(new SidecarHealthResponse(
                    Status: "ok",
                    Version: "test",
                    Gpu: null,
                    DetectorQualification: Qualification));

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => Task.FromException<PipelineHealthCheckResult>(new NotSupportedException());

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
