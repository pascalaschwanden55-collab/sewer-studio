using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSampleCommandWorkflowRequest(
    TrainingSample? Sample,
    Func<TrainingSample, TrainingSampleDecisionResult> Decide,
    Action<string> DeindexSample,
    Action<string> SetStatusText,
    Func<TrainingSample?, Task> PersistSamplesAsync);

public static class TrainingSampleCommandWorkflow
{
    public static async Task RunAsync(TrainingSampleCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sample = request.Sample;
        if (sample is null)
            return;

        var decision = request.Decide(sample);
        if (decision.ShouldDeindex)
            request.DeindexSample(sample.SampleId);

        request.SetStatusText(decision.StatusText);
        await request.PersistSamplesAsync(decision.PersistChangedSample ? sample : null).ConfigureAwait(false);
    }
}
