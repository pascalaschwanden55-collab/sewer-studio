using System.Net;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_verwendet_denselben_Plan_lokal_wenn_Sidecar_offline_ist()
    {
        var bundle = Bundle();
        var client = new FakeVisionPipelineClient
        {
            Health = new PipelineHealthCheckResult(false, false, null, null, "offline")
        };
        var local = new FakeLocalExecutor();

        var result = await CreateService(client, new FakeRequestBuilder(), local)
            .ExecuteAsync(bundle);

        Assert.Equal(TrainingExportExecutionRoute.LocalSidecarOffline, result.Route);
        Assert.Same(bundle, local.LastBundle);
        Assert.Equal("offline", result.Detail);
        Assert.Equal(0, client.ExportCalls);
    }

    [Fact]
    public async Task ExecuteAsync_umgeht_Authfehler_nicht_lokal()
    {
        var client = new FakeVisionPipelineClient
        {
            Health = new PipelineHealthCheckResult(true, false, 401, null, "Token fehlt")
        };
        var local = new FakeLocalExecutor();

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(
            () => CreateService(client, new FakeRequestBuilder(), local).ExecuteAsync(Bundle()));

        Assert.Contains("Anmeldung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, local.Calls);
        Assert.Equal(0, client.ExportCalls);
    }

    [Fact]
    public async Task ExecuteAsync_umgeht_erreichbare_aber_unbrauchbare_Health_Antwort_nicht_lokal()
    {
        var client = new FakeVisionPipelineClient
        {
            Health = new PipelineHealthCheckResult(true, true, 500, null, "HTTP 500")
        };
        var local = new FakeLocalExecutor();

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(
            () => CreateService(client, new FakeRequestBuilder(), local).ExecuteAsync(Bundle()));

        Assert.Contains("nicht betriebsbereit", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, local.Calls);
        Assert.Equal(0, client.ExportCalls);
    }

    [Fact]
    public async Task ExecuteAsync_sendet_v2_Plan_an_Sidecar_und_mappt_Bestaetigung()
    {
        var bundle = Bundle();
        var datasetRoot = DatasetRoot();
        var client = new FakeVisionPipelineClient
        {
            Response = SidecarResponse(bundle.Plan, datasetRoot, "already_complete")
        };
        var builder = new FakeRequestBuilder();
        var local = new FakeLocalExecutor();

        var result = await CreateService(client, builder, local, datasetRoot)
            .ExecuteAsync(bundle);

        Assert.Equal(TrainingExportExecutionRoute.Sidecar, result.Route);
        Assert.Equal("1.0", result.SidecarVersion);
        Assert.Equal(TrainingExportExecutionStatus.AlreadyComplete, result.Result.Status);
        Assert.Same(bundle, builder.LastBundle);
        Assert.Equal(1, client.ExportCalls);
        Assert.Equal(0, local.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_HTTP4xx_startet_keinen_lokalen_Rueckfall()
    {
        var client = new FakeVisionPipelineClient
        {
            ExportException = new SidecarBadRequestException(
                "/training/export-yolo",
                HttpStatusCode.UnprocessableEntity,
                "plan invalid")
        };
        var local = new FakeLocalExecutor();

        var error = await Assert.ThrowsAsync<SidecarBadRequestException>(
            () => CreateService(client, new FakeRequestBuilder(), local).ExecuteAsync(Bundle()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, error.StatusCode);
        Assert.Equal(0, local.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Transportausfall_verwendet_denselben_Plan_lokal()
    {
        var bundle = Bundle();
        var client = new FakeVisionPipelineClient
        {
            ExportException = new SidecarUnavailableException("Verbindung abgerissen")
        };
        var local = new FakeLocalExecutor();

        var result = await CreateService(client, new FakeRequestBuilder(), local)
            .ExecuteAsync(bundle);

        Assert.Equal(TrainingExportExecutionRoute.LocalAfterTransportFailure, result.Route);
        Assert.Same(bundle, local.LastBundle);
        Assert.Contains("abgerissen", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_mehr_als_500_Bilder_gehen_ohne_Sidecar_Request_lokal()
    {
        var bundle = Bundle(501);
        var builder = new FakeRequestBuilder();
        var client = new FakeVisionPipelineClient();
        var local = new FakeLocalExecutor();

        var result = await CreateService(client, builder, local).ExecuteAsync(bundle);

        Assert.Equal(TrainingExportExecutionRoute.LocalRequestTooLarge, result.Route);
        Assert.Same(bundle, local.LastBundle);
        Assert.Equal(0, builder.Calls);
        Assert.Equal(0, client.ExportCalls);
    }

    [Fact]
    public async Task ExecuteAsync_stoppt_bei_abweichendem_DatasetRoot()
    {
        var bundle = Bundle();
        var expectedRoot = DatasetRoot();
        var client = new FakeVisionPipelineClient
        {
            Response = SidecarResponse(bundle.Plan, DatasetRoot())
        };
        var local = new FakeLocalExecutor();

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(
            () => CreateService(client, new FakeRequestBuilder(), local, expectedRoot)
                .ExecuteAsync(bundle));

        Assert.Contains("unterschiedliche Zielordner", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, local.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_stoppt_bei_fremder_Planbestaetigung()
    {
        var bundle = Bundle();
        var datasetRoot = DatasetRoot();
        var response = SidecarResponse(bundle.Plan, datasetRoot) with
        {
            PlanId = new string('9', 64),
            PlanSha256 = new string('9', 64)
        };
        var client = new FakeVisionPipelineClient { Response = response };

        var error = await Assert.ThrowsAsync<TrainingExportPlanException>(
            () => CreateService(client, new FakeRequestBuilder(), new FakeLocalExecutor(), datasetRoot)
                .ExecuteAsync(bundle));

        Assert.Contains("aktuellen Exportplan", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TrainingExportExecutionService CreateService(
        FakeVisionPipelineClient client,
        FakeRequestBuilder builder,
        FakeLocalExecutor local,
        string? datasetRoot = null)
        => new(client, builder, local, datasetRoot ?? DatasetRoot());

    private static string DatasetRoot()
        => Path.Combine(Path.GetTempPath(), "SewerStudioTests", Guid.NewGuid().ToString("N"));

    private static TrainingExportPlanBundle Bundle(int imageCount = 1)
    {
        var classes = YoloDetectClassMapV2.Classes
            .OrderBy(item => item.Value)
            .Select(item => item.Key)
            .ToArray();
        var images = Enumerable.Range(0, imageCount)
            .Select(index =>
            {
                var hash = index.ToString("x64");
                return new TrainingExportPlannedImage(
                    hash,
                    "100-200",
                    TrainingExportTarget.Train,
                    $"img_{hash}.png",
                    [new TrainingExportPlannedLabel(
                        1,
                        "BAB_riss",
                        new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1),
                        [new TrainingExportSourceRef(
                            TrainingExportSourceType.TeacherAnnotation,
                            $"teacher-{index}")])]);
            })
            .ToArray();
        var plan = new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            "inventory-run",
            new Dictionary<string, string>
            {
                ["teacher_annotations.json"] = new string('b', 64),
                ["training_samples.json"] = new string('c', 64)
            },
            2,
            new string('d', 64),
            new string('e', 64),
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('f', 64))],
            classes,
            ["100-200"],
            [],
            new Dictionary<string, int> { ["BAB_riss"] = imageCount },
            images,
            []);
        return new TrainingExportPlanBundle(
            plan,
            images.ToDictionary(image => image.ImageSha256, _ => @"C:\frame.png"));
    }

    private static TrainingExportPlanRequestDto SidecarRequest(TrainingExportPlan plan)
        => new(
            TrainingExportPlan.CurrentSchemaVersion,
            plan.PlanId,
            plan.PlanId,
            plan.ClassMapVersion,
            plan.VsaManifestHash,
            plan.RegistryHash,
            plan.Classes,
            "e30=",
            new string('a', 64),
            []);

    private static TrainingExportPlanResponseDto SidecarResponse(
        TrainingExportPlan plan,
        string datasetRoot,
        string status = "created")
    {
        var dataset = Path.Combine(Path.GetFullPath(datasetRoot), plan.PlanId);
        return new TrainingExportPlanResponseDto(
            TrainingExportPlan.CurrentSchemaVersion,
            plan.PlanId,
            plan.PlanId,
            status,
            plan.Images.Count,
            plan.Images.Count,
            0,
            plan.Classes.Count,
            dataset,
            Path.Combine(dataset, "data.yaml"),
            Path.Combine(dataset, "manifest.json"),
            plan.Images.Select(image => image.ImageSha256).ToArray());
    }

    private static TrainingExportExecutionResult LocalResult(TrainingExportPlan plan)
        => new(
            plan.PlanId,
            plan.PlanId,
            TrainingExportExecutionStatus.Created,
            plan.Images.Count,
            plan.Images.Count,
            0,
            plan.Classes.Count,
            @"C:\dataset",
            @"C:\dataset\data.yaml",
            @"C:\dataset\manifest.json",
            plan.Images.Select(image => image.ImageSha256).ToArray());

    private sealed class FakeRequestBuilder : ITrainingExportSidecarRequestBuilder
    {
        public int Calls { get; private set; }
        public TrainingExportPlanBundle? LastBundle { get; private set; }

        public Task<TrainingExportPlanRequestDto> BuildAsync(
            TrainingExportPlanBundle bundle,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastBundle = bundle;
            return Task.FromResult(SidecarRequest(bundle.Plan));
        }
    }

    private sealed class FakeLocalExecutor : ITrainingExportPlanLocalExecutor
    {
        public int Calls { get; private set; }
        public TrainingExportPlanBundle? LastBundle { get; private set; }

        public Task<TrainingExportExecutionResult> ExecuteAsync(
            TrainingExportPlanBundle bundle,
            string datasetRoot,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastBundle = bundle;
            return Task.FromResult(LocalResult(bundle.Plan));
        }
    }

    private sealed class FakeVisionPipelineClient : IVisionPipelineClient
    {
        public PipelineHealthCheckResult Health { get; set; } = new(
            true,
            true,
            200,
            new SidecarHealthResponse("ok", "1.0", null),
            null);
        public TrainingExportPlanResponseDto? Response { get; set; }
        public Exception? ExportException { get; set; }
        public int ExportCalls { get; private set; }

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(
            CancellationToken ct = default)
            => Task.FromResult(Health);

        public Task<TrainingExportPlanResponseDto> ExportPlannedTrainingAsync(
            TrainingExportPlanRequestDto request,
            CancellationToken ct = default)
        {
            ExportCalls++;
            if (ExportException is not null)
                return Task.FromException<TrainingExportPlanResponseDto>(ExportException);
            return Task.FromResult(Response ?? throw new InvalidOperationException("Testantwort fehlt."));
        }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult(Health.Health);

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<YoloClassifyResponse> ClassifyYoloAsync(
            YoloClassifyRequest request,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
