using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

internal static class TrainingCenterRuntimeHelpers
{
    internal static async Task<string?> ExtractPreviewFrameAsync(
        TrainingCase trainingCase,
        AiRuntimeSettings cfg,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(trainingCase.VideoPath) || !File.Exists(trainingCase.VideoPath))
            return null;

        var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(trainingCase.CaseId, @"[^\w\-]", "_")}";
        try
        {
            return await FrameStore.ExtractAndStoreAsync(
                ffmpeg,
                trainingCase.VideoPath,
                2.0,
                sampleId,
                null,
                ct);
        }
        catch
        {
            return null;
        }
    }

    internal static MeterTimelineService CreateMeterTimelineService(AiRuntimeSettings cfg, int concurrency = 1)
    {
        if (!cfg.Enabled)
            return new MeterTimelineService(cfg);

        var ollamaClient = new OllamaClient(
            cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);
        var vision = new OllamaVisionFindingsService(ollamaClient, cfg.VisionModel);
        var osd = new OsdMeterDetectionService(vision);
        return new MeterTimelineService(cfg, osd, concurrency);
    }

    internal static TrainingCaseInput ToTrainingCaseInput(TrainingCase trainingCase)
        => new(
            trainingCase.CaseId,
            trainingCase.FolderPath,
            trainingCase.VideoPath,
            trainingCase.ProtocolPath,
            trainingCase.InspectionDate);

    internal static TrainingCase ToTrainingCase(TrainingCaseInput input)
        => new()
        {
            CaseId = input.CaseId,
            FolderPath = input.FolderPath,
            VideoPath = input.VideoPath,
            ProtocolPath = input.ProtocolPath,
            InspectionDate = input.InspectionDate,
            Status = TrainingCaseStatus.New,
            CreatedUtc = DateTime.UtcNow
        };

    internal static string ResolveFfmpegPath(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return "ffmpeg";

        return File.Exists(ffmpegPath) || string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            ? ffmpegPath
            : "ffmpeg";
    }

    internal static async Task<bool> CheckOllamaReachableAsync(OllamaConfig config, CancellationToken ct)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
