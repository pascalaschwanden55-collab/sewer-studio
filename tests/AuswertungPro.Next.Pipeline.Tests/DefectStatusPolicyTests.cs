using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class DefectStatusPolicyTests
{
    [Fact]
    public void GetStatus_OhneAiContext_GibtPending()
    {
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(EvOhneKontext()));
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(0.92)]
    [InlineData(0.85)]
    public void HoheConfidenceOhneSicherheitsnachweise_WirdNichtAutoAkzeptiert(double confidence)
    {
        var ev = EvMitKonfidenz(confidence);
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void VollstaendigerSicherheitsnachweis_WirdAutoAkzeptiert()
    {
        var ev = EvMitKonfidenz(
            0.94,
            qualityGate: "Green",
            kbAgreement: true,
            epistemicUncertainty: 0.10);

        Assert.Equal(DefectStatus.AutoAccepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData("Yellow", true, 0.10)]
    [InlineData("Green", false, 0.10)]
    [InlineData("Green", true, 0.16)]
    public void UnvollstaendigerOderWiderspruechlicherNachweis_BleibtPending(
        string qualityGate,
        bool kbAgreement,
        double uncertainty)
    {
        var ev = EvMitKonfidenz(0.99, qualityGate, kbAgreement, uncertainty);
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void FehlendeUnsicherheit_BleibtPending()
    {
        var ev = EvMitKonfidenz(0.99, "Green", true, epistemicUncertainty: null);
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData(0.84)]
    [InlineData(0.60)]
    public void MittlereConfidence_GibtPending(double confidence)
    {
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(EvMitKonfidenz(confidence)));
    }

    [Theory]
    [InlineData(0.59)]
    [InlineData(0.00)]
    public void NiedrigeConfidence_GibtReviewRequired(double confidence)
    {
        Assert.Equal(DefectStatus.ReviewRequired, DefectStatusPolicy.GetStatus(EvMitKonfidenz(confidence)));
    }

    [Fact]
    public void AutoApprovalThreshold_IstExakt92Prozent()
    {
        var atThreshold = EvMitKonfidenz(0.92, "Green", true, 0.15);
        var belowThreshold = EvMitKonfidenz(0.919, "Green", true, 0.10);

        Assert.Equal(DefectStatus.AutoAccepted, DefectStatusPolicy.GetStatus(atThreshold));
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(belowThreshold));
    }

    [Fact]
    public void ManuelleAkzeptanz_SchlaegtKiPolicy()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Accepted, confidence: 0.30);
        Assert.Equal(DefectStatus.Accepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void ManuelleBearbeitung_GibtAcceptedWithEdit()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.AcceptedWithEdit, confidence: 0.90);
        Assert.Equal(DefectStatus.AcceptedWithEdit, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void ManuelleAblehnung_GibtRejected()
    {
        var ev = EvMitEntscheidung(CodingUserDecision.Rejected, confidence: 0.80);
        Assert.Equal(DefectStatus.Rejected, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void CanAct_NullEvent_GibtFalse()
    {
        Assert.False(DefectStatusPolicy.CanAct(null));
    }

    [Theory]
    [InlineData(0.95)]
    [InlineData(0.70)]
    [InlineData(0.30)]
    public void CanAct_NochNichtEntschieden_GibtTrue(double confidence)
    {
        Assert.True(DefectStatusPolicy.CanAct(EvMitKonfidenz(confidence)));
    }

    [Theory]
    [InlineData(CodingUserDecision.Accepted)]
    [InlineData(CodingUserDecision.AcceptedWithEdit)]
    [InlineData(CodingUserDecision.Rejected)]
    public void CanAct_NachManuellerEntscheidung_GibtFalse(CodingUserDecision decision)
    {
        Assert.False(DefectStatusPolicy.CanAct(EvMitEntscheidung(decision)));
    }

    private static CodingEvent EvOhneKontext() =>
        new() { Entry = new ProtocolEntry { Code = "BAB" } };

    private static CodingEvent EvMitKonfidenz(
        double confidence,
        string? qualityGate = null,
        bool? kbAgreement = null,
        double? epistemicUncertainty = null) =>
        new()
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = confidence,
                QualityGateLevel = qualityGate,
                KbCodeAgreement = kbAgreement,
                EpistemicUncertainty = epistemicUncertainty,
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
