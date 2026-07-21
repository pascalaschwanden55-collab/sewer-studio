using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewSamWorkflowTests
{
    [Fact]
    public async Task Fehlende_Auswahl_startet_Sam_nicht()
    {
        var service = new FakeSegmentationService(CreateResult(CreateResponse()));
        var started = false;
        var workflow = CreateWorkflow(service, fileExists: _ => true);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            Selection: null,
            Box: new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => started = true,
            CancellationToken.None));

        Assert.Equal(TrainingReviewSamOutcome.MissingSelection, result.Outcome);
        Assert.Equal("Bitte zuerst einen Review-Kandidaten waehlen.", result.UserHint);
        Assert.False(started);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Fehlende_Box_startet_Sam_nicht()
    {
        var service = new FakeSegmentationService(CreateResult(CreateResponse()));
        var started = false;
        var workflow = CreateWorkflow(service, fileExists: _ => true);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("frame.png", "BAB"),
            Box: null,
            OnStarted: () => started = true,
            CancellationToken.None));

        Assert.Equal(TrainingReviewSamOutcome.MissingBox, result.Outcome);
        Assert.Equal("Bitte zuerst eine Box um den Schaden ziehen.", result.UserHint);
        Assert.False(started);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Fehlender_Frame_startet_Sam_nicht()
    {
        var service = new FakeSegmentationService(CreateResult(CreateResponse()));
        var started = false;
        var workflow = CreateWorkflow(service, fileExists: _ => false);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("missing.png", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => started = true,
            CancellationToken.None));

        Assert.Equal(TrainingReviewSamOutcome.MissingFrame, result.Outcome);
        Assert.Equal("Der Review-Frame ist nicht verfuegbar.", result.UserHint);
        Assert.False(started);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Leerer_Framepfad_prueft_das_Dateisystem_nicht()
    {
        var service = new FakeSegmentationService(CreateResult(CreateResponse()));
        var fileCheckCount = 0;
        var workflow = CreateWorkflow(
            service,
            fileExists: _ =>
            {
                fileCheckCount++;
                return true;
            });

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("  ", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => { },
            CancellationToken.None));

        Assert.Equal(TrainingReviewSamOutcome.MissingFrame, result.Outcome);
        Assert.Equal(0, fileCheckCount);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Erfolg_reicht_Eingaben_weiter_und_uebernimmt_die_erste_gueltige_Maske()
    {
        var response = CreateResponse(
            masks:
            [
                CreateMask(maskRle: " ", label: "leer", confidence: 0.2, area: 3),
                CreateMask(maskRle: "12,8", label: "BAB", confidence: 0.91, area: 456)
            ],
            imageWidth: 1280,
            imageHeight: 720);
        var service = new FakeSegmentationService(CreateResult(response));
        var started = 0;
        var token = new CancellationTokenSource().Token;
        var box = new BoundingBox(0.4, 0.3, 0.2, 0.1);
        var workflow = CreateWorkflow(service, diameter: 450, fileExists: _ => true);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("review.png", "BAB"),
            box,
            OnStarted: () => started++,
            token));

        Assert.Equal(TrainingReviewSamOutcome.Completed, result.Outcome);
        Assert.Equal(1, started);
        Assert.Equal(1, service.CallCount);
        Assert.Equal("review.png", service.FramePath);
        Assert.Equal("BAB", service.Code);
        Assert.Equal(box, service.Box);
        Assert.Equal(450, service.PipeDiameterMm);
        Assert.Equal(token, service.CancellationToken);
        Assert.Same(response, result.Segmentation?.Response);
        Assert.Equal("12,8", result.PendingMask?.MaskRle);
        Assert.Equal(1280, result.PendingMask?.ImageWidth);
        Assert.Equal(720, result.PendingMask?.ImageHeight);
        Assert.Equal(456, result.PendingMask?.MaskAreaPixels);
        Assert.Equal(0.91, result.PendingMask?.Confidence);
        Assert.Equal("BAB", result.PendingMask?.Label);
        Assert.Equal("SAM: 2 Maske(n) - wird mit Akzeptieren gespeichert", result.StatusText);
    }

    [Theory]
    [InlineData("Zeitueberschreitung", 2, 3, "SAM: keine Maske (Zeitueberschreitung)")]
    [InlineData(null, 2, 3, "SAM: keine Maske (2/3 Box(en) uebersprungen)")]
    [InlineData(null, 0, 0, "SAM: keine Maske")]
    public async Task Leere_Antwort_behaelt_den_bisherigen_Statustext(
        string? error,
        int skippedBoxes,
        int requestedBoxes,
        string expected)
    {
        var response = CreateResponse(
            error: error,
            skippedBoxes: skippedBoxes,
            requestedBoxes: requestedBoxes);
        var workflow = CreateWorkflow(
            new FakeSegmentationService(CreateResult(response)),
            fileExists: _ => true);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("review.png", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => { },
            CancellationToken.None));

        Assert.Equal(expected, result.StatusText);
        Assert.Null(result.PendingMask);
    }

    [Fact]
    public async Task Maske_ohne_Rle_bleibt_in_der_Anzahl_aber_wird_nicht_zum_Speichern_vorgemerkt()
    {
        var response = CreateResponse(
            masks: [CreateMask(maskRle: " ", label: "BAB", confidence: 0.4, area: 12)]);
        var workflow = CreateWorkflow(
            new FakeSegmentationService(CreateResult(response)),
            fileExists: _ => true);

        var result = await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("review.png", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => { },
            CancellationToken.None));

        Assert.Equal("SAM: 1 Maske(n) - wird mit Akzeptieren gespeichert", result.StatusText);
        Assert.Null(result.PendingMask);
    }

    [Theory]
    [InlineData(null, 300)]
    [InlineData(0, 0)]
    public async Task Nur_fehlender_Rohrdurchmesser_verwendet_300_mm(
        int? configuredDiameter,
        int expectedDiameter)
    {
        var service = new FakeSegmentationService(CreateResult(CreateResponse()));
        var workflow = CreateWorkflow(service, diameter: configuredDiameter, fileExists: _ => true);

        await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("review.png", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => { },
            CancellationToken.None));

        Assert.Equal(expectedDiameter, service.PipeDiameterMm);
    }

    [Fact]
    public async Task Startsignal_kommt_vor_Durchmesser_und_Diensterzeugung()
    {
        var started = false;
        var resolverCalled = false;
        var serviceFactoryCalled = false;
        var workflow = new TrainingReviewSamWorkflow(
            getSegmentationService: () =>
            {
                Assert.True(started);
                serviceFactoryCalled = true;
                return new FakeSegmentationService(CreateResult(CreateResponse()));
            },
            resolvePipeDiameterMm: () =>
            {
                Assert.True(started);
                resolverCalled = true;
                return 300;
            },
            fileExists: _ => true);

        await workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
            new TrainingReviewSamSelection("review.png", "BAB"),
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            OnStarted: () => started = true,
            CancellationToken.None));

        Assert.True(resolverCalled);
        Assert.True(serviceFactoryCalled);
    }

    [Fact]
    public async Task Abbruch_wird_an_das_Fenster_weitergegeben()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new FakeSegmentationService(CreateResult(CreateResponse()))
        {
            Exception = new OperationCanceledException(cts.Token)
        };
        var workflow = CreateWorkflow(service, fileExists: _ => true);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            workflow.ExecuteAsync(new TrainingReviewSamWorkflowRequest(
                new TrainingReviewSamSelection("review.png", "BAB"),
                new BoundingBox(0.5, 0.5, 0.2, 0.2),
                OnStarted: () => { },
                cts.Token)));
    }

    private static TrainingReviewSamWorkflow CreateWorkflow(
        ITrainingReviewSamSegmentationService service,
        int? diameter = 300,
        Func<string, bool>? fileExists = null)
        => new(
            getSegmentationService: () => service,
            resolvePipeDiameterMm: () => diameter,
            fileExists: fileExists);

    private static TrainingReviewSamResult CreateResult(SamResponse response)
        => new(response, Array.Empty<MaskQuantificationService.QuantifiedMask>());

    private static SamResponse CreateResponse(
        IReadOnlyList<SamMaskResult>? masks = null,
        int imageWidth = 640,
        int imageHeight = 480,
        string? error = null,
        int skippedBoxes = 0,
        int requestedBoxes = 0)
        => new(
            masks ?? Array.Empty<SamMaskResult>(),
            imageWidth,
            imageHeight,
            InferenceTimeMs: 12,
            RequestedBoxes: requestedBoxes,
            SkippedBoxes: skippedBoxes,
            Error: error);

    private static SamMaskResult CreateMask(
        string maskRle,
        string label,
        double confidence,
        int area)
        => new(
            label,
            confidence,
            Bbox: [1, 2, 3, 4],
            maskRle,
            area,
            ImageAreaPixels: 1000,
            HeightPixels: 20,
            WidthPixels: 30,
            CentroidX: 10,
            CentroidY: 12);

    private sealed class FakeSegmentationService(TrainingReviewSamResult result)
        : ITrainingReviewSamSegmentationService
    {
        public int CallCount { get; private set; }
        public string? FramePath { get; private set; }
        public BoundingBox? Box { get; private set; }
        public string? Code { get; private set; }
        public int? PipeDiameterMm { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Exception? Exception { get; init; }

        public Task<TrainingReviewSamResult> SegmentFrameFileAsync(
            string framePath,
            BoundingBox box,
            string code,
            int? pipeDiameterMm = null,
            CancellationToken ct = default)
        {
            CallCount++;
            FramePath = framePath;
            Box = box;
            Code = code;
            PipeDiameterMm = pipeDiameterMm;
            CancellationToken = ct;

            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<TrainingReviewSamResult>(Exception);
        }
    }
}
