using System;

namespace AuswertungPro.Next.Application.Player;

public interface IPlaybackTimer
{
    event EventHandler? Tick;

    bool IsEnabled { get; }

    void Start();

    void Stop();
}
