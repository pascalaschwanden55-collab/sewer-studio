namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCaseSelectionCommandRefresh(
    Action NotifyApprove,
    Action NotifyReject,
    Action NotifySetNew,
    Action NotifyGenerateSamples);

public sealed record TrainingSampleSelectionCommandRefresh(
    Action NotifyApprove,
    Action NotifyReject,
    Action NotifyRemove);

public static class TrainingSelectionCommandRefreshController
{
    public static void RefreshCaseSelection(TrainingCaseSelectionCommandRefresh refresh)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        refresh.NotifyApprove();
        refresh.NotifyReject();
        refresh.NotifySetNew();
        refresh.NotifyGenerateSamples();
    }

    public static void RefreshSampleSelection(TrainingSampleSelectionCommandRefresh refresh)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        refresh.NotifyApprove();
        refresh.NotifyReject();
        refresh.NotifyRemove();
    }
}
