using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloExportWorkflowRequest(
    Func<bool> IsBusy,
    IEnumerable<TrainingSample> Samples,
    Func<TrainingSample, bool> IsTrainingExportEligible,
    Func<Task> PersistSamplesAsync,
    Func<string?> SelectOutputDirectory,
    Func<CancellationToken> ResetCancellation,
    Func<TrainingYoloSidecarRuntime> CreateSidecarRuntime,
    Func<EvalContaminationSets> LoadEvalSets,
    Func<TrainingYoloSidecarExportPayloadRequest, Task<TrainingYoloSidecarExportPayloadResult>> BuildSidecarPayloadAsync,
    Func<TrainingYoloSidecarExportCompletionRequest, Task> RunSidecarCompletionAsync,
    Func<TrainingYoloLocalExportWorkflowRequest, Task> RunLocalExportAsync,
    Action<bool> SetBusy,
    Action<string> Log,
    Action<int> SetProgressMax,
    Action<int> SetProgressValue,
    Action<string> SetStatusText);

public static class TrainingYoloExportRequestFactory
{
    public static TrainingYoloExportWorkflowRequest CreateWithDefaults(
        IEnumerable<TrainingSample> samples,
        AppSettings? settings,
        ICodeCatalogProvider? codeCatalog,
        Func<bool> isBusy,
        Func<Task> persistSamplesAsync,
        Func<CancellationToken> resetCancellation,
        Action<bool> setBusy,
        Action<string> log,
        Action<int> setProgressMax,
        Action<int> setProgressValue,
        Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(isBusy);
        ArgumentNullException.ThrowIfNull(persistSamplesAsync);
        ArgumentNullException.ThrowIfNull(resetCancellation);
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setProgressMax);
        ArgumentNullException.ThrowIfNull(setProgressValue);
        ArgumentNullException.ThrowIfNull(setStatusText);

        return new TrainingYoloExportWorkflowRequest(
            IsBusy: isBusy,
            Samples: samples,
            IsTrainingExportEligible: sample => TrainingSampleExportEligibility.EvaluateAndUpdate(sample, codeCatalog),
            PersistSamplesAsync: persistSamplesAsync,
            SelectOutputDirectory: TrainingYoloExportTargetFolderSelector.SelectFolder,
            ResetCancellation: resetCancellation,
            CreateSidecarRuntime: TrainingYoloSidecarRuntimeFactory.CreateWithDefaults,
            LoadEvalSets: () => EvalContaminationSetProvider.Load(settings),
            BuildSidecarPayloadAsync: TrainingYoloSidecarExportPayloadWorkflow.BuildAsync,
            RunSidecarCompletionAsync: TrainingYoloSidecarExportCompletionWorkflow.RunAsync,
            RunLocalExportAsync: TrainingYoloLocalExportWorkflow.RunAsync,
            SetBusy: setBusy,
            Log: log,
            SetProgressMax: setProgressMax,
            SetProgressValue: setProgressValue,
            SetStatusText: setStatusText);
    }

}

public static class TrainingYoloExportWorkflow
{
    public static async Task RunAsync(TrainingYoloExportWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsBusy())
            return;

        var selection = TrainingYoloExportCandidateSelector.SelectWithFileSystem(
            request.Samples,
            request.IsTrainingExportEligible);
        var approved = selection.Approved;

        if (selection.RequiresPersistence)
            await request.PersistSamplesAsync().ConfigureAwait(false);

        if (approved.Count == 0)
        {
            request.SetStatusText("Keine Approved-Samples mit gueltigen Frames vorhanden.");
            request.Log("YOLO-Export: Keine exportierbaren Samples gefunden.");
            return;
        }

        var outputDir = request.SelectOutputDirectory();
        if (string.IsNullOrWhiteSpace(outputDir))
            return;

        var ct = request.ResetCancellation();

        try
        {
            request.SetBusy(true);
            request.Log($"YOLO-Export: {approved.Count} Samples \u2192 {outputDir}");
            request.SetStatusText($"YOLO-Export: {approved.Count} Samples werden vorbereitet...");

            var sidecarRuntime = request.CreateSidecarRuntime();
            var pipelineCfg = sidecarRuntime.PipelineConfig;
            var client = sidecarRuntime.Client;

            var healthCheck = await client.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
            if (!healthCheck.IsReachable)
            {
                request.Log($"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}). Versuche lokalen Export...");
                await RunLocalExportAsync(request, approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            if (!healthCheck.IsAuthorized)
            {
                var statusText = healthCheck.StatusCode is { } statusCode
                    ? $"HTTP {statusCode}"
                    : "Auth-Fehler";
                request.Log($"Sidecar erreichbar, aber Token/Auth fehlgeschlagen ({statusText}: {healthCheck.Error ?? "Token fehlt oder ist ungueltig"}). Versuche lokalen Export...");
                await RunLocalExportAsync(request, approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            var health = healthCheck.Health;
            if (health is null)
            {
                var detail = healthCheck.StatusCode is { } statusCode
                    ? $"HTTP {statusCode}"
                    : (healthCheck.Error ?? "keine Health-Antwort");
                request.Log($"Sidecar erreichbar, aber Health-Check fehlgeschlagen ({detail}). Versuche lokalen Export...");
                await RunLocalExportAsync(request, approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            request.Log($"Sidecar erreichbar: v{health.Version}, GPU: {health.Gpu?.CurrentModel ?? "?"}");

            var sidecarEvalSets = request.LoadEvalSets();
            var payload = await request.BuildSidecarPayloadAsync(
                new TrainingYoloSidecarExportPayloadRequest(
                    approved,
                    outputDir,
                    0.8,
                    sidecarEvalSets.ImageHashes,
                    sidecarEvalSets.HaltungKeys,
                    request.SetProgressMax,
                    request.SetProgressValue,
                    request.SetStatusText,
                    ct)).ConfigureAwait(false);

            if (payload.SkipEvalHash + payload.SkipEvalCase + payload.SkipNoBox > 0)
                request.Log($"  uebersprungen: {payload.SkipEvalHash} Eval-Hash, {payload.SkipEvalCase} Eval-Haltung, {payload.SkipNoBox} ohne echte Box");

            if (payload.ExportRequest.Samples.Count == 0)
            {
                request.Log("YOLO-Export: nach Eval-/Box-Filter keine Samples uebrig.");
                request.SetStatusText("YOLO-Export: keine exportierbaren Samples (Eval/Box-Filter).");
                return;
            }

            request.SetStatusText($"YOLO-Export: Sende {payload.ExportRequest.Samples.Count} Samples an Sidecar...");
            TrainingExportResponseDto response;
            try
            {
                response = await client.ExportTrainingAsync(payload.ExportRequest, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                request.Log($"Sidecar-Export nicht moeglich ({ex.Message}). Lokaler Export wird verwendet...");
                await RunLocalExportAsync(request, approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            await request.RunSidecarCompletionAsync(
                TrainingYoloSidecarExportCompletionRequestFactory.CreateWithDefaults(
                    approved,
                    response,
                    outputDir,
                    request.PersistSamplesAsync,
                    request.Log,
                    request.SetStatusText)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            request.Log("YOLO-Export abgebrochen.");
            request.SetStatusText("YOLO-Export abgebrochen.");
        }
        catch (Exception ex)
        {
            request.Log($"YOLO-Export FEHLER: {ex.Message}");
            request.SetStatusText($"YOLO-Export fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            request.SetBusy(false);
        }
    }

    private static Task RunLocalExportAsync(
        TrainingYoloExportWorkflowRequest request,
        IReadOnlyList<TrainingSample> approved,
        string outputDir,
        CancellationToken ct)
    {
        var localEvalSets = request.LoadEvalSets();
        return request.RunLocalExportAsync(
            TrainingYoloLocalExportRequestFactory.CreateWithDefaults(
                approved,
                outputDir,
                localEvalSets.ImageHashes,
                localEvalSets.HaltungKeys,
                request.PersistSamplesAsync,
                request.Log,
                request.SetProgressMax,
                request.SetProgressValue,
                request.SetStatusText,
                ct));
    }
}
