using System;

namespace AuswertungPro.Next.UI.Player;

public static class LiveDetectionFrameCaptureServiceFactory
{
    public static LiveDetectionFrameCaptureService Create(Func<string, uint, bool> takeSnapshot)
        => new(takeSnapshot);
}
