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

    // --- GetStatus: zentrale Freigabe-Regel (Ignored-Decision) ---

    [Fact]
    public void GetStatus_HoheSicherheitUndGrueneAmpel_OhneKbBeleg_GibtPending()
    {
        var ev = EvMitKonfidenz(0.99, gate: "Green");
        Assert.Equal(DefectStatus.Pending, DefectStatusPolicy.GetStatus(ev));
    }

    [Fact]
    public void GetStatus_MitUnabhaengigemKbBeleg_GibtAutoAccepted()
    {
        var ev = EvMitKonfidenz(0.99, gate: "Green", kbAgreement: true);
        Assert.Equal(DefectStatus.AutoAccepted, DefectStatusPolicy.GetStatus(ev));
    }

    [Theory]
    [InlineData(0.99, null)]     // hohe Sicherheit, aber Ampel fehlt
    [InlineData(0.99, "Yellow")] // hohe Sicherheit, aber gelbe Ampel
    [InlineData(0.85, "Green")]  // gruene Ampel, aber Sicherheit unter 0.92
    public void GetStatus_UnvollstaendigeBelege_GibtPending(double confidence, string? gate)
    {
        var ev = EvMitKonfidenz(confidence, gate);
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

    // --- CanAct ---

    [Fact]
    public void CanAct_NullEvent_GibtFalse()
    {
        Assert.False(DefectStatusPolicy.CanAct(null));
    }

    [Theory]
    [InlineData(0.90)] // Pending (hohe Sicherheit, aber ohne Ampel kein Gruen)
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

    private static CodingEvent EvMitKonfidenz(
        double confidence,
        string? gate = null,
        bool? kbAgreement = null) =>
        new()
        {
            Entry = new ProtocolEntry { Code = "BAB" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = confidence,
                QualityGateLevel = gate,
                Evidence = kbAgreement.HasValue
                    ? new CodingEventAiEvidence { KbCodeAgreement = kbAgreement }
                    : null,
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
