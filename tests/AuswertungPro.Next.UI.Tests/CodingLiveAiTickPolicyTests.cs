using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiTickPolicyTests
{
    [Fact]
    public void ShouldAnalyze_returns_true_when_live_detection_can_run()
    {
        Assert.True(InvokeShouldAnalyze(
            isClosing: false,
            hasPlayer: true,
            hasLiveDetection: true,
            CodingSessionState.Running,
            isPlayerPlaying: true));
    }

    [Fact]
    public void ShouldAnalyze_allows_missing_session_state_to_preserve_existing_timer_behavior()
    {
        Assert.True(InvokeShouldAnalyze(
            isClosing: false,
            hasPlayer: true,
            hasLiveDetection: true,
            sessionState: null,
            isPlayerPlaying: true));
    }

    [Theory]
    [InlineData(true, true, true, CodingSessionState.Running, true)]
    [InlineData(false, false, true, CodingSessionState.Running, true)]
    [InlineData(false, true, false, CodingSessionState.Running, true)]
    [InlineData(false, true, true, CodingSessionState.WaitingForUserInput, true)]
    [InlineData(false, true, true, CodingSessionState.Running, false)]
    public void ShouldAnalyze_returns_false_when_timer_should_not_run(
        bool isClosing,
        bool hasPlayer,
        bool hasLiveDetection,
        CodingSessionState sessionState,
        bool isPlayerPlaying)
    {
        Assert.False(InvokeShouldAnalyze(
            isClosing,
            hasPlayer,
            hasLiveDetection,
            sessionState,
            isPlayerPlaying));
    }

    private static bool InvokeShouldAnalyze(
        bool isClosing,
        bool hasPlayer,
        bool hasLiveDetection,
        CodingSessionState? sessionState,
        bool isPlayerPlaying)
    {
        return CodingLiveAiTickPolicy.ShouldAnalyze(
            isClosing,
            hasPlayer,
            hasLiveDetection,
            sessionState,
            isPlayerPlaying);
    }
}
