using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionOsdMeterStatusWorkflowOutcome
{
    Shown,
    ScheduledReset
}

public sealed record LiveDetectionOsdMeterStatusWorkflowRequest(
    string Message,
    bool ResetAfterDelay);

public sealed record LiveDetectionOsdMeterStatusWorkflowActions(
    Action<string> ShowMessage,
    Action<TimeSpan, Action> ScheduleReset,
    Func<double?> GetLastMeter,
    Action<double> ShowMeter,
    Action HideBadge);

public sealed record LiveDetectionOsdMeterStatusDisplayActions(
    Action<string> ShowMessage,
    Func<double?> GetLastMeter,
    Action<double> ShowMeter,
    Action HideBadge);

public sealed record LiveDetectionOsdMeterStatusWorkflowResult(
    LiveDetectionOsdMeterStatusWorkflowOutcome Outcome)
{
    public bool ScheduledReset => Outcome == LiveDetectionOsdMeterStatusWorkflowOutcome.ScheduledReset;
}

public static class LiveDetectionOsdMeterStatusWorkflow
{
    public static LiveDetectionOsdMeterStatusWorkflowResult Show(
        LiveDetectionOsdMeterStatusWorkflowRequest request,
        LiveDetectionOsdMeterStatusDisplayActions displayActions)
    {
        ArgumentNullException.ThrowIfNull(displayActions);

        return Show(
            request,
            new LiveDetectionOsdMeterStatusWorkflowActions(
                ShowMessage: displayActions.ShowMessage,
                ScheduleReset: (delay, reset) =>
                {
                    var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(delay, reset);
                    resetTimer.Start();
                },
                GetLastMeter: displayActions.GetLastMeter,
                ShowMeter: displayActions.ShowMeter,
                HideBadge: displayActions.HideBadge));
    }

    public static LiveDetectionOsdMeterStatusWorkflowResult Show(
        LiveDetectionOsdMeterStatusWorkflowRequest request,
        LiveDetectionOsdMeterStatusWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ShowMessage(request.Message);

        if (!request.ResetAfterDelay)
            return new LiveDetectionOsdMeterStatusWorkflowResult(
                LiveDetectionOsdMeterStatusWorkflowOutcome.Shown);

        actions.ScheduleReset(TimeSpan.FromSeconds(3), () =>
        {
            var lastMeter = actions.GetLastMeter();
            if (lastMeter.HasValue)
                actions.ShowMeter(lastMeter.Value);
            else
                actions.HideBadge();
        });

        return new LiveDetectionOsdMeterStatusWorkflowResult(
            LiveDetectionOsdMeterStatusWorkflowOutcome.ScheduledReset);
    }
}
