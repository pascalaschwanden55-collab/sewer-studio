using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiButtonDisplayPolicyTests
{
    [Fact]
    public void ActiveColor_is_existing_live_ai_green()
    {
        Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E), CodingLiveAiButtonDisplayPolicy.ActiveColor);
    }

    [Fact]
    public void BlinkColor_alternates_between_active_and_dark_green()
    {
        Assert.Equal(CodingLiveAiButtonDisplayPolicy.ActiveColor, CodingLiveAiButtonDisplayPolicy.BlinkColor(true));
        Assert.Equal(Color.FromRgb(0x16, 0x65, 0x34), CodingLiveAiButtonDisplayPolicy.BlinkColor(false));
    }

    [Fact]
    public void BuildStatus_formats_active_live_ai_state()
    {
        var result = CodingLiveAiButtonDisplayPolicy.BuildStatus(isActive: true, "qwen3-vl");

        Assert.Equal("Automatische KI-Analyse aktiv", result.StatusText);
        Assert.Equal("Intervall alle 5 Sekunden | qwen3-vl", result.DetailText);
    }

    [Fact]
    public void BuildStatus_formats_ready_state()
    {
        var result = CodingLiveAiButtonDisplayPolicy.BuildStatus(isActive: false, "qwen3-vl");

        Assert.Equal("K\u00fcnstliche Intelligenz bereit", result.StatusText);
        Assert.Equal("Modell: qwen3-vl", result.DetailText);
    }
}
