using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDefectStatusDisplayPolicyTests
{
    [Theory]
    [InlineData(DefectStatus.AutoAccepted, "Auto-Akzeptiert (Green Zone)", "\u2713")]
    [InlineData(DefectStatus.Pending, "Review empfohlen (Yellow Zone)", "\u23F3")]
    [InlineData(DefectStatus.ReviewRequired, "Manuell erforderlich (Red Zone)", "\u26A0")]
    [InlineData(DefectStatus.Accepted, "Akzeptiert", "\u2713")]
    [InlineData(DefectStatus.AcceptedWithEdit, "Bearbeitet", "\u270E")]
    [InlineData(DefectStatus.Rejected, "Abgelehnt", "\u2717")]
    public void Text_and_icon_mappings_match_existing_status_contract(
        DefectStatus status,
        string text,
        string icon)
    {
        Assert.Equal(text, CodingDefectStatusDisplayPolicy.DisplayText(status));
        Assert.Equal(icon, CodingDefectStatusDisplayPolicy.StatusIcon(status));
    }

    [Fact]
    public void ZoneDotColor_uses_accept_edit_reject_and_open_colors()
    {
        Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E),
            CodingDefectStatusDisplayPolicy.ZoneDotColor(DefectStatus.Accepted));
        Assert.Equal(Color.FromRgb(0x3B, 0x82, 0xF6),
            CodingDefectStatusDisplayPolicy.ZoneDotColor(DefectStatus.AcceptedWithEdit));
        Assert.Equal(Color.FromRgb(0xEF, 0x44, 0x44),
            CodingDefectStatusDisplayPolicy.ZoneDotColor(DefectStatus.Rejected));
        Assert.Equal(Color.FromRgb(0x94, 0xA3, 0xB8),
            CodingDefectStatusDisplayPolicy.ZoneDotColor(DefectStatus.Pending));
    }
}
