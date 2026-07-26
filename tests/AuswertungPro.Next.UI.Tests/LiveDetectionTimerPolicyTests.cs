using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTimerPolicyTests
{
    [Fact]
    public void ShouldRunTick_returns_true_when_detection_can_run()
    {
        Assert.True(InvokeShouldRunTick(
            isClosing: false,
            hasPlayer: true,
            isDetectionInFlight: false,
            hasLiveDetectionService: true,
            hasDetectionCancellation: true,
            isPlayerPlaying: true,
            hasPendingFindings: false));
    }

    [Theory]
    [InlineData(true, true, false, true, true, true, false)]
    [InlineData(false, false, false, true, true, true, false)]
    [InlineData(false, true, true, true, true, true, false)]
    [InlineData(false, true, false, false, true, true, false)]
    [InlineData(false, true, false, true, false, true, false)]
    [InlineData(false, true, false, true, true, false, false)]
    [InlineData(false, true, false, true, true, true, true)]
    public void ShouldRunTick_returns_false_when_timer_must_wait_or_stop(
        bool isClosing,
        bool hasPlayer,
        bool isDetectionInFlight,
        bool hasLiveDetectionService,
        bool hasDetectionCancellation,
        bool isPlayerPlaying,
        bool hasPendingFindings)
    {
        Assert.False(InvokeShouldRunTick(
            isClosing,
            hasPlayer,
            isDetectionInFlight,
            hasLiveDetectionService,
            hasDetectionCancellation,
            isPlayerPlaying,
            hasPendingFindings));
    }

    private static bool InvokeShouldRunTick(
        bool isClosing,
        bool hasPlayer,
        bool isDetectionInFlight,
        bool hasLiveDetectionService,
        bool hasDetectionCancellation,
        bool isPlayerPlaying,
        bool hasPendingFindings)
    {
        return LiveDetectionTimerPolicy.ShouldRunTick(
            isClosing,
            hasPlayer,
            isDetectionInFlight,
            hasLiveDetectionService,
            hasDetectionCancellation,
            isPlayerPlaying,
            hasPendingFindings);
    }
}
