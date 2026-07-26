using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingEventFactoryTests
{
    [Fact]
    public void Create_builds_protocol_entry_and_ai_context()
    {
        var videoTime = TimeSpan.FromSeconds(12);
        var gate = Gate(0.82);

        var draft = CodingLiveFindingEventFactory.Create(
            "BCAEB",
            "Anschluss",
            Finding("lateral connection"),
            meter: 4.2,
            videoTime,
            gate);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCAEB", draft.Entry.Code);
        Assert.Equal("Anschluss", draft.Entry.Beschreibung);
        Assert.Equal(4.2, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Equal("BCAEB", draft.AiContext.SuggestedCode);
        Assert.Equal(0.82, draft.AiContext.Confidence);
        Assert.Equal("lateral connection", draft.AiContext.Reason);
        // Kein echter Modellwert am Finding -> kein Qwen-Signal (Severity ist keine
        // Confidence mehr; Fehlerpruefung 11.07., Kritisch 3).
        Assert.Null(draft.AiContext.Evidence!.QwenVisionConf);
        Assert.Equal(0.6, draft.AiContext.Evidence.PlausibilityScore);
        Assert.Equal("BCAEB", draft.AiContext.Evidence.DamageCategory);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void Create_falls_back_to_finding_label_when_official_label_is_missing()
    {
        var draft = CodingLiveFindingEventFactory.Create(
            "BAB",
            officialLabel: null,
            Finding("crack"),
            meter: 1.0,
            videoTime: TimeSpan.Zero,
            Gate(0.5));

        Assert.Equal("crack", draft.Entry.Beschreibung);
    }

    [Fact]
    public void Create_applies_code_meta_and_overlay_builders()
    {
        var draft = CodingLiveFindingEventFactory.Create(
            "BCAEB",
            "Anschluss",
            Finding(
                "connection",
                clock: "3:00",
                intrusionPercent: 12,
                x1: 0.7,
                y1: 0.4,
                x2: 0.9,
                y2: 0.6),
            meter: 2.0,
            videoTime: TimeSpan.FromSeconds(8),
            Gate(0.9));

        Assert.Equal("3:00", draft.Entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("12", draft.Entry.CodeMeta.Parameters["vsa.querschnitt.prozent"]);
        Assert.NotNull(draft.Overlay);
        Assert.Equal(OverlayToolType.Rectangle, draft.Overlay.ToolType);
        Assert.Equal(4, draft.Overlay.Points.Count);
    }

    private static QualityGateResult Gate(double confidence)
        => new(
            confidence,
            TrafficLight.Yellow,
            new Dictionary<string, double>(),
            "test");

    private static LiveFrameFinding Finding(
        string label,
        string? clock = null,
        int? intrusionPercent = null,
        double? x1 = null,
        double? y1 = null,
        double? x2 = null,
        double? y2 = null)
        => new(
            Label: label,
            Severity: 2,
            PositionClock: clock,
            ExtentPercent: null,
            VsaCodeHint: null,
            IntrusionPercent: intrusionPercent,
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
