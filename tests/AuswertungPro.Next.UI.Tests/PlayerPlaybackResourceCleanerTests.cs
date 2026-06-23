using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackResourceCleanerTests
{
    [Fact]
    public void StopPlayer_invokes_stop_action()
    {
        var method = FindCleanerMethod("StopPlayer", typeof(Action));
        Assert.NotNull(method);

        var called = false;

        method.Invoke(null, [new Action(() => called = true)]);

        Assert.True(called);
    }

    [Fact]
    public void DetachVideoView_swallows_detach_errors()
    {
        var method = FindCleanerMethod("DetachVideoView", typeof(Action));
        Assert.NotNull(method);

        method.Invoke(null, [new Action(() => throw new InvalidOperationException("detach failed"))]);
    }

    [Fact]
    public void DisposeMediaPlayer_logs_dispose_errors()
    {
        var method = FindCleanerMethod("DisposeMediaPlayer", typeof(IDisposable), typeof(Action<string>));
        Assert.NotNull(method);
        var messages = new List<string>();

        method.Invoke(null, [
            new ThrowingDisposable("media failed"),
            new Action<string>(messages.Add)
        ]);

        Assert.Contains(messages, message => message.Contains("MediaPlayer Dispose error: media failed", StringComparison.Ordinal));
    }

    [Fact]
    public void DisposeLibVlc_logs_dispose_errors()
    {
        var method = FindCleanerMethod("DisposeLibVlc", typeof(IDisposable), typeof(Action<string>));
        Assert.NotNull(method);
        var messages = new List<string>();

        method.Invoke(null, [
            new ThrowingDisposable("libvlc failed"),
            new Action<string>(messages.Add)
        ]);

        Assert.Contains(messages, message => message.Contains("LibVLC Dispose error: libvlc failed", StringComparison.Ordinal));
    }

    private static MethodInfo? FindCleanerMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerWindowTimerFactory).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerPlaybackResourceCleaner")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);

    private sealed class ThrowingDisposable(string message) : IDisposable
    {
        public void Dispose()
            => throw new InvalidOperationException(message);
    }
}
