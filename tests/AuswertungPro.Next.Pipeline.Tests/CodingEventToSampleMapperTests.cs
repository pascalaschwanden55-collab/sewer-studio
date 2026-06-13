using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingEventToSampleMapperTests
{
    [Fact]
    public void OhneKiKontext_setzt_Status_New_und_nicht_Approved()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BABAC" },
            AiContext = null,
            MeterAtCapture = 12.3
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId: "case-1", framePath: null);

        Assert.Equal(TrainingSampleStatus.New, sample.Status);
    }

    [Fact]
    public void OhneKiKontext_setzt_keinen_ExactMatch()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BABAC" },
            AiContext = null,
            MeterAtCapture = 12.3
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId: "case-1", framePath: null);

        Assert.Null(sample.MatchLevel);
    }

    [Fact]
    public void MitKiKontext_Accepted_setzt_Status_Approved()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BABAC" },
            AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted },
            MeterAtCapture = 12.3
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId: "case-1", framePath: null);

        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
    }

    [Fact]
    public void MitAufnahmedatumAb2022_ist_trainingsfaehig()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BABAC" },
            AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted },
            MeterAtCapture = 12.3
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(
            ev,
            caseId: "case-1",
            framePath: null,
            inspectionDate: new DateTime(2022, 1, 1));

        Assert.True(sample.TrainingEligible);
        Assert.Null(sample.TrainingEligibilityReason);
    }

    private static CodingEvent BuildEvent(CodingUserDecision decision, string? qg = null)
        => new()
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BCA", Beschreibung = "x" },
            MeterAtCapture = 12.3,
            VideoTimestamp = System.TimeSpan.FromSeconds(5),
            AiContext = new CodingEventAiContext { SuggestedCode = "BCA", Confidence = 0.8, Decision = decision, QualityGateLevel = qg }
        };

    [Fact]
    public void FromCodingEvent_Accept_SetztHumanConfirmedTrueOhneCorrected()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(
            BuildEvent(CodingUserDecision.Accepted, qg: "Green"), "H1", null, null,
            confirmedByUser: "tester",
            confirmedAtUtc: new System.DateTime(2026, 6, 13, 9, 0, 0, System.DateTimeKind.Utc));

        Assert.Equal(true, s.HumanConfirmed);
        Assert.Equal(false, s.Corrected);
        Assert.Equal("tester", s.ConfirmedByUser);
        Assert.Equal("Green", s.QualityGateLevel);
    }

    [Fact]
    public void FromCodingEvent_Edit_SetztCorrectedTrue()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(BuildEvent(CodingUserDecision.AcceptedWithEdit), "H1", null, null);
        Assert.Equal(true, s.HumanConfirmed);
        Assert.Equal(true, s.Corrected);
    }

    [Fact]
    public void FromCodingEvent_Reject_HumanConfirmedFalse_StatusRejected()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(BuildEvent(CodingUserDecision.Rejected), "H1", null, null);
        Assert.Equal(false, s.HumanConfirmed);
        Assert.Equal(TrainingSampleStatus.Rejected, s.Status);
    }

    [Fact]
    public void FromCodingEvent_OhneAiContext_HumanConfirmedNull()
    {
        var ev = new CodingEvent
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BCA" },
            MeterAtCapture = 1, VideoTimestamp = System.TimeSpan.Zero
        };
        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);
        Assert.Null(s.HumanConfirmed);
        Assert.Null(s.Corrected);
    }
}
