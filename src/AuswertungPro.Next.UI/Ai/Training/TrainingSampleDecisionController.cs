using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSampleDecisionResult(
    string StatusText,
    bool ShouldDeindex,
    bool PersistChangedSample);

public static class TrainingSampleDecisionController
{
    public static TrainingSampleDecisionResult Approve(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        sample.Status = TrainingSampleStatus.Approved;
        return new TrainingSampleDecisionResult(
            $"Approved: {sample.SampleId}",
            ShouldDeindex: false,
            PersistChangedSample: true);
    }

    public static TrainingSampleDecisionResult Reject(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        sample.Status = TrainingSampleStatus.Rejected;
        sample.KbIndexState = KbIndexState.None;
        return new TrainingSampleDecisionResult(
            $"Rejected: {sample.SampleId}",
            ShouldDeindex: true,
            PersistChangedSample: false);
    }

    public static TrainingSampleDecisionResult Remove(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        sample.Status = TrainingSampleStatus.Removed;
        sample.KbIndexState = KbIndexState.None;
        return new TrainingSampleDecisionResult(
            $"Entfernt: {sample.SampleId}",
            ShouldDeindex: true,
            PersistChangedSample: false);
    }
}
