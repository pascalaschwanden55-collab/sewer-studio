using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiOverlayDisplayPolicyTests
{
    [Theory]
    [InlineData(CodingUserDecision.Accepted, 0x22, 0xC5, 0x5E)]
    [InlineData(CodingUserDecision.AcceptedWithEdit, 0x22, 0xC5, 0x5E)]
    [InlineData(CodingUserDecision.Rejected, 0xEF, 0x44, 0x44)]
    [InlineData(CodingUserDecision.Ignored, 0xF5, 0x9E, 0x0B)]
    public void StrokeColor_maps_decision_to_existing_overlay_colors(
        CodingUserDecision decision,
        byte r,
        byte g,
        byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), CodingAiOverlayDisplayPolicy.StrokeColor(decision));
    }

    [Theory]
    [InlineData("BBA", 0.812, "BBA [81.2%]")]
    [InlineData("BBA", null, "BBA")]
    [InlineData("", 0.5, "? [50.0%]")]
    [InlineData(null, null, "?")]
    public void LabelText_formats_code_and_confidence(string? code, double? confidence, string expected)
    {
        Assert.Equal(expected, CodingAiOverlayDisplayPolicy.LabelText(code, confidence));
    }
}
