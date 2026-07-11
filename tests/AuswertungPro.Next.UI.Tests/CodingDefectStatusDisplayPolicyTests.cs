using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDefectStatusDisplayPolicyTests
{
    [Theory]
    [InlineData(DefectStatus.AutoAccepted, "KI-Kriterien erfüllt", "\u2713")]
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

    [Fact]
    public void BuildInlineDetail_formats_ai_event_and_action_state()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" },
            MeterAtCapture = 1.234,
            AiContext = new CodingEventAiContext
            {
                // AutoAccepted verlangt Gate-Werte plus unabhaengigen KB-Abgleich.
                Confidence = 0.94,
                QualityGateLevel = "Green",
                Evidence = new CodingEventAiEvidence { KbCodeAgreement = true },
                Decision = CodingUserDecision.Ignored
            }
        };

        var state = CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev);

        Assert.Equal("BAB", state.CodeText);
        Assert.Equal("Riss", state.DescriptionText);
        Assert.Equal("1.23m", state.DistanceText);
        Assert.Equal("94%", state.ConfidenceText);
        Assert.Equal(0.94, state.Confidence);
        Assert.Equal(DefectStatus.AutoAccepted, state.Status);
        Assert.Equal("KI-Kriterien erfüllt", state.StatusText);
        Assert.True(state.CanAct);
    }

    [Fact]
    public void BuildInlineDetail_formats_manual_event_without_confidence()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" },
            MeterAtCapture = 2
        };

        var state = CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev);

        Assert.Equal("BCA", state.CodeText);
        Assert.Equal("Anschluss", state.DescriptionText);
        Assert.Equal("2.00m", state.DistanceText);
        Assert.Equal("\u2013", state.ConfidenceText);
        Assert.Null(state.Confidence);
        Assert.Equal(DefectStatus.Pending, state.Status);
        Assert.True(state.CanAct);
    }
}
