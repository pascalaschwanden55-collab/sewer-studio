using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingApprovedProtocolExportWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IEnumerable<TrainingSample> Samples,
    Func<TrainingSample, bool> IsExportEligible,
    Action<ProtocolEntry, string?> AddProtocolTrainingSample,
    Func<Task> PersistSamplesAsync,
    Func<DateTime> UtcNow,
    string TargetPath,
    Action<string> Log,
    Action<string> SetStatusText);

public static class TrainingApprovedProtocolExportWorkflow
{
    public static async Task RunAsync(TrainingApprovedProtocolExportWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        try
        {
            request.SetIsBusy(true);
            var result = await TrainingApprovedProtocolExportController.RunAsync(
                request.Samples.ToList(),
                request.IsExportEligible,
                request.AddProtocolTrainingSample,
                request.PersistSamplesAsync,
                request.UtcNow,
                request.TargetPath).ConfigureAwait(false);

            foreach (var line in result.LogLines)
                request.Log(line);

            request.SetStatusText(result.StatusText);
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }
}
