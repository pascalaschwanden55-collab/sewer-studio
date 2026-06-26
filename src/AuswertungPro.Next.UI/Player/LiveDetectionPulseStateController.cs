using System;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class LiveDetectionPulseStateController
{
    public bool IsRunning { get; private set; }

    public LiveDetectionPulseStartActions CreateStartActions(Action startPulse)
    {
        ArgumentNullException.ThrowIfNull(startPulse);

        return new LiveDetectionPulseStartActions(
            SetRunning,
            startPulse);
    }

    public LiveDetectionPulseStopActions CreateStopActions(Action stopPulse)
    {
        ArgumentNullException.ThrowIfNull(stopPulse);

        return new LiveDetectionPulseStopActions(
            ClearRunning,
            stopPulse);
    }

    private void SetRunning()
        => IsRunning = true;

    private void ClearRunning()
        => IsRunning = false;
}
