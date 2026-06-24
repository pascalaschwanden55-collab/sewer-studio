using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerSnapshotPauseRestorer
{
    public static void ResumeIfNeeded(
        bool wasPlaying,
        bool closing,
        bool playbackDisposed,
        Action resumePlayback)
    {
        if (!wasPlaying || closing || playbackDisposed)
            return;

        AuswertungPro.Next.Application.Common.BestEffort.Try(
            resumePlayback,
            "VLC: Pause aufheben");
    }

    public static void ResumeIfNeeded(
        bool wasPlaying,
        bool closing,
        bool playbackDisposed,
        Action<bool> setPause)
        => ResumeIfNeeded(wasPlaying, closing, playbackDisposed, () => setPause(false));
}
