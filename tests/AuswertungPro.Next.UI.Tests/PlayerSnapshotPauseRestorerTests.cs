using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotPauseRestorerTests
{
    [Fact]
    public void ResumeIfNeeded_resumes_when_snapshot_paused_playback_and_window_is_active()
    {
        var method = FindResumeIfNeededMethod();
        Assert.NotNull(method);
        var called = false;

        method.Invoke(null, [true, false, false, new Action(() => called = true)]);

        Assert.True(called);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void ResumeIfNeeded_skips_resume_when_not_allowed(bool wasPlaying, bool closing, bool playbackDisposed)
    {
        var method = FindResumeIfNeededMethod();
        Assert.NotNull(method);
        var called = false;

        method.Invoke(null, [wasPlaying, closing, playbackDisposed, new Action(() => called = true)]);

        Assert.False(called);
    }

    [Fact]
    public void ResumeIfNeeded_swallows_resume_errors()
    {
        var method = FindResumeIfNeededMethod();
        Assert.NotNull(method);

        method.Invoke(null, [true, false, false, new Action(() => throw new InvalidOperationException("resume failed"))]);
    }

    private static MethodInfo? FindResumeIfNeededMethod()
        => typeof(PlayerSnapshotPathPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerSnapshotPauseRestorer")
            ?.GetMethod(
                "ResumeIfNeeded",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(bool), typeof(bool), typeof(bool), typeof(Action)],
                modifiers: null);
}
