using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace SidecarE2eSmoke;

public sealed class SidecarSmokeRunner
{
    public async Task<SidecarSmokeReport> RunAsync(
        SidecarSmokeOptions options,
        CancellationToken ct = default)
    {
        var report = new SidecarSmokeReport
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            SidecarUrl = options.SidecarUrl,
            Source = options.SourceDescription,
            FullPipeline = options.FullPipeline,
        };

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSec) };
            var client = new VisionPipelineClient(new Uri(options.SidecarUrl), http, options.Token);
            await using var sidecarLease = await SidecarProcessLease.EnsureReadyAsync(options, client, ct);
            report.SidecarStartedByTool = sidecarLease.StartedByTool;

            await RunHealthAsync(client, report, ct);
            var frames = await FfmpegFrameExtractor.ExtractAsync(options, ct);
            report.AddCheck(
                "video_frame_decode",
                frames.Count > 0 && frames.All(frame => frame.Bytes.Length > 0),
                $"{frames.Count} Bild(er), {frames.Sum(frame => frame.Bytes.Length)} Bytes");

            var firstFrame = frames[0];
            var imageBase64 = Convert.ToBase64String(firstFrame.Bytes);
            await RunDirectModelChecksAsync(client, options, imageBase64, report, ct);

            if (options.FullPipeline)
                await RunProductionPipelineAsync(client, options, frames, report, ct);

            if (options.FullPipeline)
            {
                var contractPath = options.ResolveGoldenPath();
                var contract = PipelineGoldenContract.Load(contractPath);
                report.GoldenValidation = GoldenContractValidator.Validate(report, contract, contractPath);
            }

            report.Success = report.Error is null
                             && report.Checks.All(check => check.Passed)
                             && report.GoldenValidation is not { Success: false };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            report.Error = ex.ToString();
            report.Success = false;
            report.AddCheck("runtime", false, ex.Message);
        }

        return report;
    }

    private static async Task RunHealthAsync(
        IVisionPipelineClient client,
        SidecarSmokeReport report,
        CancellationToken ct)
    {
        Console.WriteLine("Health pruefen...");
        var health = await client.CheckHealthDetailedAsync(ct);
        report.Health = new HealthReport(
            health.IsReachable,
            health.IsAuthorized,
            health.StatusCode,
            health.Error,
            health.Health?.Status,
            health.Health?.Version,
            health.Health?.Gpu?.VramAllocatedGb,
            health.Health?.Gpu?.VramTotalGb,
            health.Health?.Gpu?.LoadedModels?.Keys.OrderBy(key => key).ToArray() ?? []);

        report.AddCheck(
            "health",
            health.IsReachable && health.IsAuthorized && health.Health is not null && health.Error is null,
            health.Error ?? $"Sidecar {health.Health?.Version}, HTTP {health.StatusCode}");
    }

    private static async Task RunDirectModelChecksAsync(
        IVisionPipelineClient client,
        SidecarSmokeOptions options,
        string imageBase64,
        SidecarSmokeReport report,
        CancellationToken ct)
    {
        Console.WriteLine("YOLO-Klassifikation pruefen...");
        var classify = await client.ClassifyYoloAsync(
            new YoloClassifyRequest(imageBase64, options.TopK), ct);
        report.Classify = classify;
        var predictions = classify.Predictions
                          ?? throw new InvalidDataException("YOLO-Klassifikation lieferte keine Vorhersage-Liste.");
        report.AddCheck(
            "classify",
            classify.ClassifierLoaded
            && IsValidTime(classify.InferenceTimeMs),
            $"geladen={classify.ClassifierLoaded}, Vorhersagen={predictions.Count}");

        Console.WriteLine("YOLO-Erkennung pruefen...");
        var yolo = await client.DetectYoloAsync(
            new YoloRequest(imageBase64, options.YoloConfidence), ct);
        report.Yolo = yolo;
        var yoloDetections = yolo.Detections
                             ?? throw new InvalidDataException("YOLO lieferte keine Erkennungsliste.");
        report.AddCheck(
            "yolo",
            !string.IsNullOrWhiteSpace(yolo.FrameClass)
            && IsValidTime(yolo.InferenceTimeMs),
            $"relevant={yolo.IsRelevant}, Boxen={yoloDetections.Count}, Modell={yolo.ModelName ?? "?"}");

        if (options.ShouldRunDino)
        {
            Console.WriteLine("DINO-Erkennung pruefen...");
            var dino = await client.DetectDinoAsync(
                new DinoRequest(
                    imageBase64,
                    options.DinoPrompt,
                    options.DinoBoxThreshold,
                    options.DinoTextThreshold),
                ct);
            report.Dino = dino;
            var dinoDetections = dino.Detections
                                 ?? throw new InvalidDataException("DINO lieferte keine Erkennungsliste.");
            report.AddCheck(
                "dino",
                !dino.Degraded
                && string.IsNullOrWhiteSpace(dino.Error)
                && IsValidTime(dino.InferenceTimeMs),
                dino.Error ?? $"Boxen={dinoDetections.Count}, degraded={dino.Degraded}");
        }

        if (!options.ShouldRunSam)
            return;

        Console.WriteLine("SAM-Segmentierung pruefen...");
        var samBoxes = BuildSamBoxes(report.Dino, yolo, options.ShouldUseSamFallbackBox);
        if (samBoxes.Count == 0)
        {
            report.SamSkippedReason = "Keine DINO-/YOLO-Box vorhanden und keine Ersatzbox erlaubt.";
            report.AddCheck("sam", false, report.SamSkippedReason);
            return;
        }

        var sam = await client.SegmentSamAsync(
            new SamRequest(imageBase64, samBoxes, options.PipeDiameterMm), ct);
        report.Sam = sam;
        var samMasks = sam.Masks
                       ?? throw new InvalidDataException("SAM lieferte keine Maskenliste.");
        var samContractOk = sam.ImageWidth > 0
                            && sam.ImageHeight > 0
                            && string.IsNullOrWhiteSpace(sam.Error)
                            && IsValidTime(sam.InferenceTimeMs);
        report.AddCheck(
            "sam",
            samContractOk,
            sam.Error
            ?? $"Masken={samMasks.Count}, degraded={sam.Degraded}, ausgelassen={sam.SkippedBoxes}");

        report.QuantifiedMasks = samMasks
            .Select(mask => MaskQuantificationService.Quantify(
                mask,
                sam.ImageWidth,
                sam.ImageHeight,
                options.PipeDiameterMm ?? 300))
            .ToArray();
        var quantificationOk = report.QuantifiedMasks.Count == samMasks.Count
                               && report.QuantifiedMasks.All(IsValidQuantification);
        report.AddCheck(
            "quantification",
            quantificationOk,
            $"{report.QuantifiedMasks.Count} Maske(n) mit gueltigen Wertebereichen");
    }

    private static async Task RunProductionPipelineAsync(
        IVisionPipelineClient client,
        SidecarSmokeOptions options,
        IReadOnlyList<ExtractedFrame> frames,
        SidecarSmokeReport report,
        CancellationToken ct)
    {
        Console.WriteLine("Produktive Mehrmodell-Verarbeitung pruefen...");
        var service = new SingleFrameMultiModelService(
            client,
            options.YoloConfidence,
            options.DinoBoxThreshold,
            options.DinoTextThreshold);
        var frameReports = new List<FramePipelineReport>(frames.Count);
        var reachLength = Math.Max(3.0, (frames.Count - 1) * options.FrameStepSeconds);

        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            var result = await service.AnalyzeFrameAsync(
                frame.Bytes,
                options.PipeDiameterMm ?? 300,
                ct: ct,
                currentMeterM: (frame.Index - 1) * options.FrameStepSeconds,
                reachLengthM: reachLength);
            frameReports.Add(new FramePipelineReport(
                frame.Index,
                frame.TimestampSec,
                frame.Bytes.Length,
                result.IsRelevant,
                result.DinoDetections.Count,
                result.SamResponse?.Masks.Count ?? 0,
                result.QuantifiedMasks.Count,
                result.TotalTimeMs,
                result.Error));
        }

        report.Frames = frameReports;
        report.AddCheck(
            "production_pipeline",
            frameReports.Count == frames.Count && frameReports.All(frame => string.IsNullOrWhiteSpace(frame.Error)),
            $"{frameReports.Count}/{frames.Count} Videobilder ohne Verarbeitungsfehler");
    }

    private static IReadOnlyList<SamBoundingBox> BuildSamBoxes(
        DinoResponse? dino,
        YoloResponse yolo,
        bool allowFallbackBox)
    {
        var dinoBox = dino?.Detections.OrderByDescending(item => item.Confidence).FirstOrDefault();
        if (dinoBox is not null)
        {
            return
            [
                new SamBoundingBox(
                    dinoBox.X1,
                    dinoBox.Y1,
                    dinoBox.X2,
                    dinoBox.Y2,
                    dinoBox.Label,
                    dinoBox.Confidence)
            ];
        }

        var yoloBox = yolo.Detections.OrderByDescending(item => item.Confidence).FirstOrDefault();
        if (yoloBox is not null)
        {
            return
            [
                new SamBoundingBox(
                    yoloBox.X1,
                    yoloBox.Y1,
                    yoloBox.X2,
                    yoloBox.Y2,
                    yoloBox.ClassName,
                    yoloBox.Confidence)
            ];
        }

        return allowFallbackBox
            ? [new SamBoundingBox(0.2, 0.2, 0.8, 0.8, "contract_probe", 1.0)]
            : [];
    }

    private static bool IsValidTime(double value) => double.IsFinite(value) && value >= 0;

    private static bool IsValidQuantification(MaskQuantificationService.QuantifiedMask item)
        => IsPercent(item.ExtentPercent)
           && IsPercent(item.CrossSectionReductionPercent)
           && IsPercent(item.IntrusionPercent)
           && item.HeightMm is null or >= 0
           && item.WidthMm is null or >= 0;

    private static bool IsPercent(int? value) => value is null or >= 0 and <= 100;
}
