using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdTimerPolicyTests
{
    [Fact]
    public void ShouldReadMeter_returns_true_when_osd_timer_can_run()
    {
        Assert.True(InvokeShouldReadMeter(
            isClosing: false,
            hasPlayer: true,
            isCodingMode: true,
            isOsdReading: false,
            isAnalyzing: false,
            hasLiveDetection: true));
    }

    [Theory]
    [InlineData(true, true, true, false, false, true)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, true, true, true, false, true)]
    [InlineData(false, true, true, false, true, true)]
    [InlineData(false, true, true, false, false, false)]
    public void ShouldReadMeter_returns_false_when_osd_timer_must_not_run(
        bool isClosing,
        bool hasPlayer,
        bool isCodingMode,
        bool isOsdReading,
        bool isAnalyzing,
        bool hasLiveDetection)
    {
        Assert.False(InvokeShouldReadMeter(
            isClosing,
            hasPlayer,
            isCodingMode,
            isOsdReading,
            isAnalyzing,
            hasLiveDetection));
    }

    private static bool InvokeShouldReadMeter(
        bool isClosing,
        bool hasPlayer,
        bool isCodingMode,
        bool isOsdReading,
        bool isAnalyzing,
        bool hasLiveDetection)
    {
        return CodingOsdTimerPolicy.ShouldReadMeter(
            isClosing,
            hasPlayer,
            isCodingMode,
            isOsdReading,
            isAnalyzing,
            hasLiveDetection);
    }
}
