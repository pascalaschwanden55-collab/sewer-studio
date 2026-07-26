using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloExportWorkflowRequest(
    Func<bool> IsBusy,
    IReadOnlyList<TrainingSample> Samples,
    ITrainingYoloExportCoordinator Coordinator,
    Func<CancellationToken> ResetCancellation,
    Func<DateTimeOffset> UtcNow,
    Action<bool> SetBusy,
    Action<string> Log,
    Action<int> SetProgressMax,
    Action<int> SetProgressValue,
    Action<string> SetStatusText);

public static class TrainingYoloExportRequestFactory
{
    public static TrainingYoloExportWorkflowRequest Create(
        IEnumerable<TrainingSample> samples,
        TrainingYoloExportDependencies dependencies,
        Func<bool> isBusy,
        Func<CancellationToken> resetCancellation,
        Action<bool> setBusy,
        Action<string> log,
        Action<int> setProgressMax,
        Action<int> setProgressValue,
        Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(isBusy);
        ArgumentNullException.ThrowIfNull(resetCancellation);
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setProgressMax);
        ArgumentNullException.ThrowIfNull(setProgressValue);
        ArgumentNullException.ThrowIfNull(setStatusText);

        return new TrainingYoloExportWorkflowRequest(
            IsBusy: isBusy,
            Samples: samples.ToArray(),
            Coordinator: dependencies.Coordinator,
            ResetCancellation: resetCancellation,
            UtcNow: () => DateTimeOffset.UtcNow,
            SetBusy: setBusy,
            Log: log,
            SetProgressMax: setProgressMax,
            SetProgressValue: setProgressValue,
            SetStatusText: setStatusText);
    }
}

/// <summary>
/// Duenne UI-Huelle: Busy-, Fortschritts- und Fehlermeldungen bleiben hier.
/// Inventar, Plan, Sidecar-/Lokalwahl und Abschluss liegen im injizierten Koordinator.
/// </summary>
public static class TrainingYoloExportWorkflow
{
    public static async Task RunAsync(TrainingYoloExportWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsBusy())
            return;

        var cancellationToken = request.ResetCancellation();
        try
        {
            request.SetBusy(true);
            request.SetProgressMax(0);
            request.SetProgressValue(0);
            request.SetStatusText("YOLO-Export: Datenbestand wird geprueft...");

            var progress = new UiContextProgress<TrainingYoloExportProgress>(value =>
                ApplyProgress(request, value));
            var result = await request.Coordinator.RunAsync(
                    new TrainingYoloExportCommand(
                        request.UtcNow(),
                        UpdateTargets: request.Samples),
                    progress,
                    cancellationToken);

            if (result.Status == TrainingYoloExportResultStatus.NoImages)
            {
                const string emptyMessage =
                    "YOLO-Export: Der gepruefte Plan enthaelt keine exportierbaren Bilder.";
                request.Log(emptyMessage);
                request.SetStatusText(emptyMessage);
                return;
            }

            var execution = result.Execution
                            ?? throw new InvalidOperationException(
                                "Der Export wurde ohne Ausfuehrungsergebnis abgeschlossen.");
            request.SetProgressMax(result.Plan.Images.Count);
            request.SetProgressValue(result.Plan.Images.Count);
            var message =
                $"YOLO-Export fertig: {execution.Result.TotalImages} Bilder, " +
                $"{result.Plan.Classes.Count} feste Klassen -> {execution.Result.DatasetPath}";
            request.Log(message);
            request.Log($"  Plan: {result.Plan.PlanId}");
            request.Log($"  Weg: {DescribeRoute(execution.Route)}");
            request.Log($"  Markierte TrainingSamples: {result.Completion.MarkedTrainingSamples}");
            var statusMessage = message;
            if (result.RegistryGateSkippedSampleIds is { Count: > 0 } registryGateSkipped)
            {
                request.Log(
                    $"  Hinweis: {registryGateSkipped.Count} vollstaendige Goldsamples nicht im " +
                    $"Freigaberegister - nicht exportiert: {string.Join(", ", registryGateSkipped)}");
                statusMessage =
                    $"{message} | Hinweis: {registryGateSkipped.Count} Goldsamples nicht im Freigaberegister.";
            }

            request.SetStatusText(statusMessage);
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

    private static void ApplyProgress(
        TrainingYoloExportWorkflowRequest request,
        TrainingYoloExportProgress progress)
    {
        if (progress.Total is { } total)
            request.SetProgressMax(total);
        request.SetProgressValue(progress.Processed);
        request.Log(progress.Message);
        request.SetStatusText(progress.Message);
    }

    private static string DescribeRoute(TrainingExportExecutionRoute route) => route switch
    {
        TrainingExportExecutionRoute.Sidecar => "Sidecar",
        TrainingExportExecutionRoute.LocalSidecarOffline => "lokal (Sidecar offline)",
        TrainingExportExecutionRoute.LocalRequestTooLarge => "lokal (Plan zu gross fuer einen Request)",
        TrainingExportExecutionRoute.LocalAfterTransportFailure => "lokal (Verbindung abgebrochen)",
        _ => route.ToString()
    };

    private sealed class UiContextProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;
        private readonly SynchronizationContext? _context;

        public UiContextProgress(Action<T> report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _context = SynchronizationContext.Current;
        }

        public void Report(T value)
        {
            if (_context is null || ReferenceEquals(_context, SynchronizationContext.Current))
            {
                _report(value);
                return;
            }

            _context.Post(_ => _report(value), null);
        }
    }
}
