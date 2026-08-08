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
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BCA",
                Confidence = 0.8,
                Decision = decision,
                QualityGateLevel = qg,
                CentralDecision = new AiDecisionAudit
                {
                    Outcome = "AutoAccept",
                    ReasonCode = "EvidenceConfirmed",
                    PolicyVersion = "test-v2"
                }
            }
        };

    [Fact]
    public void FromCodingEvent_Accept_SetztHumanConfirmedTrueOhneCorrected()
    {
        var ev = BuildEvent(CodingUserDecision.Accepted, qg: "Green");
        ev.AiContext!.SamMaskRle = "0,1000,50,306150";
        ev.AiContext.SamMaskImageWidth = 640;
        ev.AiContext.SamMaskImageHeight = 480;
        ev.AiContext.Evidence = new CodingEventAiEvidence { SamMaskStability = 0.91 };
        var s = CodingEventToSampleMapper.FromCodingEvent(
            ev, "H1", "gold.png", null,
            confirmedByUser: "tester",
            confirmedAtUtc: new System.DateTime(2026, 6, 13, 9, 0, 0, System.DateTimeKind.Utc));

        Assert.Equal(true, s.HumanConfirmed);
        Assert.Equal(false, s.Corrected);
        Assert.Equal("tester", s.ConfirmedByUser);
        Assert.Equal("Green", s.QualityGateLevel);
        Assert.Equal(SourceTypeNames.ManualCoding, s.SourceType);
        Assert.Equal(MatchLevelNames.ReviewApproved, s.MatchLevel);
        Assert.Equal("BCA - x", s.Beschreibung);
        Assert.Equal("0,1000,50,306150", s.SamMaskRle);
        Assert.Equal(640, s.SamMaskImageWidth);
        Assert.Equal(480, s.SamMaskImageHeight);
        Assert.Equal(0.91, s.SamMaskConfidence);
        Assert.Equal("BCA", s.SamMaskLabel);
        Assert.Equal("test-v2", s.CentralDecision!.PolicyVersion);
        Assert.NotSame(ev.AiContext!.CentralDecision, s.CentralDecision);
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

    [Fact]
    public void ManuellerAccept_IstGoldAberKeinKiTreffer()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BCA",
                Beschreibung = "Anschluss",
                Source = ProtocolEntrySource.Manual
            },
            ReviewContext = new CodingEventReviewContext
            {
                Decision = CodingUserDecision.Accepted,
                Reason = "Manuell bestaetigt"
            },
            MeterAtCapture = 1.2
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(
            ev,
            "H1",
            "gold.png",
            confirmedByUser: "tester",
            confirmedAtUtc: new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.Equal(true, sample.HumanConfirmed);
        Assert.Null(sample.KiCode);
        Assert.Equal(MatchLevelNames.ReviewApproved, sample.MatchLevel);
        Assert.Equal(false, sample.Corrected);
        Assert.Equal(SourceTypeNames.ManualCoding, sample.SourceType);
        Assert.True(ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, "tester"));
    }

    [Fact]
    public void FromCodingEvent_TrenntRohbildUndMarkiertesBeweisbild()
    {
        var sample = CodingEventToSampleMapper.FromCodingEvent(
            BuildEvent(CodingUserDecision.Accepted),
            caseId: "H1",
            framePath: @"C:\frames\raw.png",
            evidenceFramePath: @"C:\frames\annotated.png");

        Assert.Equal(@"C:\frames\raw.png", sample.FramePath);
        Assert.Equal(@"C:\frames\annotated.png", sample.EvidenceFramePath);
        Assert.Null(sample.AdditionalFramePaths);
    }

    [Theory]
    [InlineData("1,2,3")]            // Laufsumme 5 statt 10x10=100
    [InlineData("0,10,5,80")]        // Laufsumme 95 statt 10x10=100
    [InlineData("0,100")]            // Leermaske: 100 Hintergrund-, 0 Masken-Pixel
    public void FromCodingEvent_formal_defekte_SamMask_wird_nicht_uebernommen(string rle)
    {
        // Gold-Wahrheits-Haertung: formal defekte Masken bleiben weg — das Sample bleibt
        // sichtbar unvollstaendig und landet in 'Unvollstaendige Goldframes'.
        var ev = BuildEvent(CodingUserDecision.Accepted);
        ev.AiContext!.SamMaskRle = rle;
        ev.AiContext.SamMaskImageWidth = 10;
        ev.AiContext.SamMaskImageHeight = 10;
        ev.AiContext.Evidence = new CodingEventAiEvidence { SamMaskStability = 0.9 };

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", "gold.png", null);

        Assert.Null(s.SamMaskRle);
        Assert.Null(s.SamMaskImageWidth);
        Assert.Null(s.SamMaskImageHeight);
        Assert.Null(s.SamMaskConfidence);
        Assert.Null(s.SamMaskLabel);
        Assert.False(s.HasSamMask);
    }

    [Fact]
    public void FromCodingEvent_formatgueltige_SamMask_wird_uebernommen()
    {
        var ev = BuildEvent(CodingUserDecision.Accepted);
        ev.AiContext!.SamMaskRle = "0,10,5,85";   // 10x10, 5 Masken-Pixel
        ev.AiContext.SamMaskImageWidth = 10;
        ev.AiContext.SamMaskImageHeight = 10;
        ev.AiContext.Evidence = new CodingEventAiEvidence { SamMaskStability = 0.9 };

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", "gold.png", null);

        Assert.Equal("0,10,5,85", s.SamMaskRle);
        Assert.Equal(10, s.SamMaskImageWidth);
        Assert.Equal(10, s.SamMaskImageHeight);
        Assert.Equal(0.9, s.SamMaskConfidence);
        Assert.Equal("BCA", s.SamMaskLabel);
        Assert.True(s.HasSamMask);
    }

    [Fact]
    public void FromCodingEvent_manuelle_Overlay_Segmentierung_bleibt_ohne_KiKontext_erhalten()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BCC",
                Beschreibung = "Bogen",
                Source = ProtocolEntrySource.Manual
            },
            ReviewContext = new CodingEventReviewContext
            {
                Decision = CodingUserDecision.Accepted
            },
            Overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.5, 0.6)],
                SamMask = new OverlaySamMask
                {
                    MaskRle = "0,10,5,85",
                    ImageWidth = 10,
                    ImageHeight = 10,
                    MaskAreaPixels = 5,
                    Confidence = 0.92,
                    Label = "manuell"
                }
            },
            MeterAtCapture = 4.2
        };

        var sample = CodingEventToSampleMapper.FromCodingEvent(
            ev,
            "H1",
            "gold.png",
            confirmedByUser: "tester",
            confirmedAtUtc: new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc));

        Assert.Null(ev.AiContext);
        Assert.Equal(SourceTypeNames.ManualCoding, sample.SourceType);
        Assert.Equal("0,10,5,85", sample.SamMaskRle);
        Assert.Equal(10, sample.SamMaskImageWidth);
        Assert.Equal(10, sample.SamMaskImageHeight);
        Assert.Equal(5, sample.SamMaskAreaPixels);
        Assert.Equal(0.92, sample.SamMaskConfidence);
        Assert.Equal("BCC", sample.SamMaskLabel);
        Assert.True(sample.HasSamMask);
    }

    [Fact]
    public void FromCodingEvent_mit_Box_enthaelt_die_Signatur_einen_Geometrie_Teil()
    {
        // Mehrfachobjekt: Box (0.1/0.2)-(0.5/0.6) -> Zentrum 0.3/0.4, Breite/Hoehe 0.4.
        var ev = BuildEvent(CodingUserDecision.Accepted);
        ev.Overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint> { new(0.1, 0.2), new(0.5, 0.6) }
        };

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);

        Assert.Equal("H1|BCA|12.3|12.3|b:0.300,0.400,0.400,0.400", s.Signature);
    }

    [Fact]
    public void FromCodingEvent_ohne_Box_behhaelt_die_Signatur_das_4_Teiler_Format()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(
            BuildEvent(CodingUserDecision.Accepted), "H1", null, null);

        Assert.Equal("H1|BCA|12.3|12.3", s.Signature);
    }

    [Fact]
    public void Ohne_KiKontext_gilt_das_Sample_als_unabhaengig_codiert()
    {
        // Nur solche Samples duerfen spaeter ein Modell messen.
        var ev = new CodingEvent
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BAJC" },
            MeterAtCapture = 3.0,
            AiContext = null
        };

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);

        Assert.Equal(
            TrainingSampleSuggestionOrigin.Independent,
            s.SuggestionProvenance?.Origin);
        Assert.True(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(s));
    }

    [Fact]
    public void Mit_KiKontext_wird_der_sichtbare_Vorschlag_festgehalten()
    {
        var ev = BuildEvent(CodingUserDecision.Accepted);

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);

        var herkunft = s.SuggestionProvenance;
        Assert.Equal(TrainingSampleSuggestionOrigin.SuggestionShown, herkunft?.Origin);
        Assert.Equal("BCA", herkunft?.SuggestedCode);
        Assert.Equal(0.8, herkunft?.SuggestedConfidence);
        Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(s));
    }

    [Fact]
    public void Das_vorschlagende_Modell_wird_mitgeschrieben()
    {
        // Ohne Modellbindung laesst sich spaeter nicht sagen, welches Modell
        // welche Daten beeinflusst hat.
        var ev = BuildEvent(CodingUserDecision.Accepted);
        ev.AiContext!.SuggestedByModelId = "bcc_nc15_seed44_20260808";
        ev.AiContext!.SuggestedByModelSha256 = new string('a', 64);

        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);

        Assert.Equal("bcc_nc15_seed44_20260808", s.SuggestionProvenance?.ModelId);
        Assert.Equal(new string('a', 64), s.SuggestionProvenance?.ModelSha256);
        Assert.Contains(
            "bcc_nc15_seed44_20260808",
            SuggestionProvenancePolicy.DescribeMeasurementBias(s));
    }
}
