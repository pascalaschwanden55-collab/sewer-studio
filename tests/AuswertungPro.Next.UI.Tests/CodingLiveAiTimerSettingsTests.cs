using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiTimerSettingsTests
{
    [Fact]
    public void Settings_keep_current_live_ai_intervals()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), CodingLiveAiTimerSettings.AnalysisInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(800), CodingLiveAiTimerSettings.BlinkInterval);
    }

    [Fact]
    public void FormatAnalysisIntervalText_uses_analysis_interval()
    {
        Assert.Equal("Intervall alle 5 Sekunden", CodingLiveAiTimerSettings.FormatAnalysisIntervalText());
    }
}
