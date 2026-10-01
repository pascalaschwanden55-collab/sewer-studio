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
    public async Task Selected_candidate_is_pinned_by_id_and_sha()
    {
        var pipeline = new FakePipelineClient
        {
            ResponseCandidateId = "bcc_bogen_b50b37ab8a4f",
            ResponseCandidateSha = "a" + new string('0', 63),
        };
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ => [1, 2, 3]);

        var result = await service.DetectBccCandidateAsync(
            @"C:\frames\bogen.jpg",
            "bcc_bogen_b50b37ab8a4f",
            "a" + new string('0', 63));

        Assert.True(result.Available);
        Assert.NotNull(pipeline.LastCandidateRequest);
        Assert.Equal(
            "bcc_bogen_b50b37ab8a4f",
            pipeline.LastCandidateRequest.CandidateId);
        Assert.Equal(
            "a" + new string('0', 63),
            pipeline.LastCandidateRequest.CandidateSha256);
    }

    [Theory]
    [InlineData("bcc_bogen_anders", null)]
    [InlineData(null, "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public async Task Selected_candidate_discards_boxes_when_response_pin_differs(
        string? differentId,
        string? differentSha)
    {
        const string requestedId = "bcc_bogen_b50b37ab8a4f";
        var requestedSha = "a" + new string('0', 63);
        var pipeline = new FakePipelineClient
        {
            ResponseCandidateId = differentId ?? requestedId,
            ResponseCandidateSha = differentSha ?? requestedSha,
        };
        var service = new TrainingPreviewDetectionService(
            pipeline,
            _ => [1, 2, 3]);

        var result = await service.DetectBccCandidateAsync(
            @"C:\frames\bogen.jpg",
            requestedId,
            requestedSha);

        Assert.False(result.Available);
        Assert.Empty(result.Detections);
        Assert.Contains("anderen BCC-Kandidaten", result.Error);
    }

    [Fact]
    public async Task Candidate_preserves_unusable_frame_reason_without_detections()
    {
        const string candidateId = "bcc_bogen_b50b37ab8a4f";
        var candidateSha = "a" + new string('0', 63);
        var pipeline = new FakePipelineClient
        {
            ResponseCandidateId = candidateId,
            ResponseCandidateSha = candidateSha,
            ResponseFrameUsable = false,
            ResponseQualityReason = "zu dunkel",
        };
        var service = new TrainingPreviewDetectionService(pipeline, _ => [1, 2, 3]);

        var result = await service.DetectBccCandidateAsync(
            @"C:\frames\bogen.jpg",
            candidateId,
            candidateSha);

        Assert.True(result.Available);
        Assert.False(result.FrameUsable);
        Assert.Equal("zu dunkel", result.QualityReason);
        Assert.Empty(result.Detections);
    }

    [Fact]
    public async Task Candidate_catalog_contains_no_paths()
    {
        var pipeline = new FakePipelineClient();
        var service = new TrainingPreviewDetectionService(pipeline);

        var result = await service.GetBccCandidatesAsync();

        Assert.True(result.Available);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("bcc_bogen_b50b37ab8a4f", candidate.CandidateId);
        Assert.Equal("a" + new string('0', 63), candidate.CandidateSha256);
    }

    [Fact]
    public async Task Default_candidate_pin_never_falls_back_to_unpinned_detection()
    {
        ITrainingPreviewDetectionService service = new LegacyPreviewDetectionService();

        var result = await service.DetectBccCandidateAsync(
            @"C:\frames\bogen.jpg",
            "bcc_bogen_b50b37ab8a4f",
            "a" + new string('0', 63));

        Assert.False(result.Available);
        Assert.Empty(result.Detections);
        Assert.Contains("exakte", result.Error);
        Assert.Equal(0, ((LegacyPreviewDetectionService)service).DetectCalls);
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
        public string ResponseCandidateId { get; set; } = "bcc_candidate";
        public string ResponseCandidateSha { get; set; } = "test-sha";
        public bool ResponseFrameUsable { get; set; } = true;
        public string? ResponseQualityReason { get; set; }
        public BccTestYoloRequest? LastCandidateRequest { get; private set; }

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
                CandidateId: ResponseCandidateId,
                CandidateSha256: ResponseCandidateSha,
                ModelName: "bcc_test",
                Device: "cpu",
                FrameUsable: ResponseFrameUsable,
                QualityReason: ResponseQualityReason));
        }

        public Task<BccTestYoloResponse> DetectBccTestYoloAsync(
            BccTestYoloRequest request,
            CancellationToken ct = default)
        {
            LastCandidateRequest = request;
            return DetectBccTestYoloAsync(
                new YoloRequest(request.ImageBase64, request.ConfidenceThreshold),
                ct);
        }

        public Task<BccTestCandidatesResponse> GetBccTestCandidatesAsync(
            CancellationToken ct = default)
            => Task.FromResult(new BccTestCandidatesResponse(
                Available: true,
                Error: null,
                Candidates:
                [
                    new BccTestCandidateInfo(
                        "bcc_bogen_b50b37ab8a4f",
                        "a" + new string('0', 63),
                        Map50: 0.74,
                        EpochsCompleted: 40,
                        CreatedUtc: "2026-07-28T14:43:21Z")
                ]));

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

    private sealed class LegacyPreviewDetectionService : ITrainingPreviewDetectionService
    {
        public int DetectCalls { get; private set; }

        public Task<TrainingPreviewDetectionResult> DetectAsync(
            string framePath,
            TrainingPreviewModelKind modelKind,
            double confidenceThreshold = 0.25,
            CancellationToken cancellationToken = default)
        {
            DetectCalls++;
            return Task.FromResult(new TrainingPreviewDetectionResult(
                Available: true,
                Error: null,
                modelKind,
                ModelName: "unpinned",
                ModelSha256: "unpinned",
                Detections: [],
                InferenceTimeMs: 0));
        }

        public Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<TrainingDetectorQualification?>(null);
    }
}
