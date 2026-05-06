using System;

namespace AuswertungPro.Next.Application.Player;

public enum PlaybackDispatcherPriority
{
    ApplicationIdle
}

public interface IPlaybackDispatcher
{
    void BeginInvoke(Action action, PlaybackDispatcherPriority priority);
}
