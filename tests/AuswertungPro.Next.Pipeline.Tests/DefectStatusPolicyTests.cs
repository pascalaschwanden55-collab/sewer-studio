using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="DefectStatusPolicy"/>.
/// Prueft die Konfidenz-Schwellwerte und die Benutzerentscheidungs-Logik.
/// </summary>
public sealed class DefectStatusPolicyTests
{
    // --- GetStatus: kein AiContext ---

    [Fact]
    public void GetStatus_OhneAiContext_GibtPending()
    {
        var ev = EvOhneKontext();
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    // --- GetStatus: Konfidenz-Zonen (Ignored-Decision) ---

    [Theory]
    [InlineData(1.00)]
    [InlineData(0.85)]
    public void GetStatus_KonfidenzGreenZone_GibtAutoAccepted(double confidence)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.Equal(DefectStatus.AutoAccepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData(0.84)]
    [InlineData(0.60)]
    public void GetStatus_KonfidenzYellowZone_GibtPending(double confidence)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData(0.59)]
    [InlineData(0.00)]
    public void GetStatus_KonfidenzRedZone_GibtReviewRequired(double confidence)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.Equal(DefectStatus.ReviewRequired, DefectStatusPolicy.GetStatus(ev));
    }

    // --- GetStatus: Manuelle Entscheidung schlaegt Konfidenz-Zone ---

    [Fact]
    public void GetStatus_ManuelleAkzeptanz_GibtAccepted()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Accepted, confidence: 0.30);
        Assert.Equal(DefectStatus.Accepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void GetStatus_ManuelleBearbeitung_GibtAcceptedWithEdit()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.AcceptedWithEdit, confidence: 0.90);
        Assert.Equal(DefectStatus.AcceptedWithEdit, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void GetStatus_ManuelleAblehnung_GibtRejected()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Rejected, confidence: 0.80);
        Assert.Equal(DefectStatus.Rejected, DefectStatusPolicy.GetStatus(ev));
    }

    // --- GetStatus: Schwellwert-Grenzen exakt ---

    [Theory]
    [InlineData(0.85, DefectStatus.AutoAccepted)]   // Exakt Green-Grenze
    [InlineData(0.60, DefectStatus.Pending)]         // Exakt Yellow-Grenze
    [InlineData(0.599, DefectStatus.ReviewRequired)] // Knapp darunter
    public void GetStatus_Schwellwertgrenzen_SindKorrekt(double confidence, DefectStatus erwartet)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.Equal(erwartet, DefectStatusPolicy.GetStatus(ev));
    }

    // --- CanAct ---

    [Fact]
    public void CanAct_NullEvent_GibtFalse()
    {
        Assert.False(DefectStatusPolicy.CanAct(null));
    }

    [Theory]
    [InlineData(0.90)] // AutoAccepted
    [InlineData(0.70)] // Pending
    [InlineData(0.30)] // ReviewRequired
    public void CanAct_NochNichtEntschieden_GibtTrue(double confidence)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.True(DefectStatusPolicy.CanAct(ev));
    }

    [Fact]
    public void CanAct_NachManueller_Akzeptanz_GibtFalse()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Accepted);
        Assert.False(DefectStatusPolicy.CanAct(ev));
    }

    [Fact]
    public void CanAct_NachAblehnung_GibtFalse()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Rejected);
        Assert.False(DefectStatusPolicy.CanAct(ev));
    }

    [Fact]
    public void CanAct_NachBearbeitung_GibtFalse()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.AcceptedWithEdit);
        Assert.False(DefectStatusPolicy.CanAct(ev));
    }

    // --- Hilfsmethoden ---

    private static CodingEvent EvOhneKontext() =>
        new() { Entry = new ProtocolEntry { Code = "BAB" } };

    private static CodingEvent EvMitKonfidenz(double confidence) =>
        new()
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = confidence,
                Decision = CodingUserDecision.Ignored
            }
        };

    private static CodingEvent EvMitEntscheidung(
        CodingUserDecision decision,
        double confidence = 0.80) =>
        new()
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = confidence,
                Decision = decision
            }
        };
}
