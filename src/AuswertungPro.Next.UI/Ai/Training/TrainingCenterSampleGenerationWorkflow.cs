using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterSampleGenerationWorkflowRequest(
    TrainingCase? SelectedCase,
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<CancellationToken> ResetCancellation,
    Func<IDisposable> BeginActivity,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Func<TrainingCaseInput, IReadOnlyCollection<string>, CancellationToken, Task<TrainingSampleGenerationResult>> GenerateWithDiagnosticsAsync,
    Func<List<TrainingSample>, Task> SaveSamplesAsync,
    Action<IReadOnlyList<TrainingSample>> AppendSamples,
    Action<string> SetStatusText);

public static class TrainingCenterSampleGenerationWorkflow
{
    public static async Task RunAsync(TrainingCenterSampleGenerationWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selectedCase = request.SelectedCase;
        if (selectedCase is null || request.GetIsBusy())
            return;

        var ct = request.ResetCancellation();
        using var _ = request.BeginActivity();

        try
        {
            request.SetIsBusy(true);
            request.SetStatusText($"Generiere Samples für {selectedCase.CaseId}...");

            var existing = await request.LoadSamplesAsync().ConfigureAwait(false);
            var existingSigs = existing
                .Select(s => s.Signature)
                .ToHashSet(StringComparer.Ordinal);

            var generation = await request.GenerateWithDiagnosticsAsync(
                ToTrainingCaseInput(selectedCase),
                existingSigs,
                ct).ConfigureAwait(false);
            var newSamples = generation.Samples;

            if (newSamples.Count == 0)
            {
                request.SetStatusText(TrainingCenterSampleGenerationStatusFormatter.FormatEmptyCaseStatus(
                    selectedCase.CaseId,
                    selectedCase.ProtocolPath,
                    generation));
                return;
            }

            await request.SaveSamplesAsync(newSamples).ConfigureAwait(false);
            request.AppendSamples(newSamples);
            request.SetStatusText($"{newSamples.Count} neue Samples generiert für {selectedCase.CaseId}.");
        }
        catch (OperationCanceledException)
        {
            request.SetStatusText("Sample-Generierung abgebrochen.");
        }
        catch (Exception ex)
        {
            request.SetStatusText($"Fehler bei Sample-Generierung: {ex.Message}");
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }

    private static TrainingCaseInput ToTrainingCaseInput(TrainingCase trainingCase)
        => new(
            trainingCase.CaseId,
            trainingCase.FolderPath,
            trainingCase.VideoPath,
            trainingCase.ProtocolPath,
            trainingCase.InspectionDate);
}

