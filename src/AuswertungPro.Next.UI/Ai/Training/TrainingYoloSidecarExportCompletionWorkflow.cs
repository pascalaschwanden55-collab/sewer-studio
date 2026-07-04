using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloSidecarExportCompletionRequest(
    IReadOnlyList<TrainingSample> ApprovedSamples,
    TrainingExportResponseDto Response,
    string OutputDir,
    Func<Task> PersistSamplesAsync,
    Action<string> Log,
    Action<string> SetStatusText,
    Func<DateTime> UtcNow);

public static class TrainingYoloSidecarExportCompletionRequestFactory
{
    public static TrainingYoloSidecarExportCompletionRequest CreateWithDefaults(
        IReadOnlyList<TrainingSample> approvedSamples,
        TrainingExportResponseDto response,
        string outputDir,
        Func<Task> persistSamplesAsync,
        Action<string> log,
        Action<string> setStatusText)
        => new(
            ApprovedSamples: approvedSamples,
            Response: response,
            OutputDir: outputDir,
            PersistSamplesAsync: persistSamplesAsync,
            Log: log,
            SetStatusText: setStatusText,
            UtcNow: () => DateTime.UtcNow);
}

public static class TrainingYoloSidecarExportCompletionWorkflow
{
    public static async Task RunAsync(TrainingYoloSidecarExportCompletionRequest request)
    {
        var exportedUtc = request.UtcNow();
        foreach (var sample in request.ApprovedSamples)
            sample.ExportedUtc = exportedUtc;

        await request.PersistSamplesAsync().ConfigureAwait(false);

        var response = request.Response;
        var message = $"YOLO-Export fertig: {response.TotalSamples} Samples " +
                      $"({response.TrainCount} Train, {response.ValCount} Val), " +
                      $"{response.ClassesUsed.Count} Klassen \u2192 {request.OutputDir}";

        request.Log(message);
        request.Log($"  data.yaml: {response.DataYamlPath}");
        request.Log($"  Klassen: {string.Join(", ", response.ClassesUsed)}");
        request.SetStatusText(message);
    }
}
