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
}
