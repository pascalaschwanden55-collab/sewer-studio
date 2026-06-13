using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingSampleMergeTests
{
    [Fact]
    public void ApplyUpdatableFields_uebernimmt_Status_KbState_und_BBox()
    {
        var t = new TrainingSample { SampleId = "x", Status = TrainingSampleStatus.New };
        var s = new TrainingSample
        {
            SampleId = "x", Status = TrainingSampleStatus.Approved, KbIndexState = KbIndexState.Pending,
            MatchLevel = "ReviewApproved", Notes = "geprueft",
            BboxXCenter = 0.5, BboxYCenter = 0.4, BboxWidth = 0.3, BboxHeight = 0.2
        };

        TrainingSampleMerge.ApplyUpdatableFields(t, s);

        Assert.Equal(TrainingSampleStatus.Approved, t.Status);
        Assert.Equal(KbIndexState.Pending, t.KbIndexState);
        Assert.Equal("ReviewApproved", t.MatchLevel);
        Assert.True(t.HasBbox);
        Assert.Equal(0.5, t.BboxXCenter);
        Assert.Equal(0.2, t.BboxHeight);
    }

    [Fact]
    public void ApplyUpdatableFields_behaelt_bestehende_BBox_wenn_Source_keine_hat()
    {
        var t = new TrainingSample { SampleId = "x", BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2 };
        var s = new TrainingSample { SampleId = "x", Status = TrainingSampleStatus.Approved };

        TrainingSampleMerge.ApplyUpdatableFields(t, s);

        Assert.True(t.HasBbox);          // bestehende Box bleibt
        Assert.Equal(TrainingSampleStatus.Approved, t.Status);
    }

    [Fact]
    public void ApplyUpdatableFields_ueberschreibt_SourceType_nur_wenn_gesetzt()
    {
        var t = new TrainingSample { SampleId = "x", SourceType = "PdfPhoto" };
        var s = new TrainingSample { SampleId = "x", SourceType = null };

        TrainingSampleMerge.ApplyUpdatableFields(t, s);

        Assert.Equal("PdfPhoto", t.SourceType); // null in source -> bestehender Wert bleibt
    }

    [Fact]
    public void ApplyUpdatableFields_UebernimmtGoldFelder()
    {
        var target = new TrainingSample { SampleId = "s1", Code = "BCA" };
        var source = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            HumanConfirmed = true, Corrected = true,
            ConfirmedByUser = "tester",
            ConfirmedAtUtc = new System.DateTime(2026, 6, 13, 9, 0, 0, System.DateTimeKind.Utc),
            QualityGateLevel = "Green"
        };

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.True(target.HumanConfirmed);
        Assert.True(target.Corrected);
        Assert.Equal("tester", target.ConfirmedByUser);
        Assert.Equal("Green", target.QualityGateLevel);
    }

    [Fact]
    public void ApplyUpdatableFields_EntwertetGesetztesGoldNichtBeiTeilUpdate()
    {
        var target = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            HumanConfirmed = true, Corrected = false,
            ConfirmedByUser = "tester", QualityGateLevel = "Green"
        };
        var source = new TrainingSample { SampleId = "s1", Code = "BCA" }; // HumanConfirmed/Corrected = null

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.True(target.HumanConfirmed);
        Assert.Equal("tester", target.ConfirmedByUser);
        Assert.Equal("Green", target.QualityGateLevel);
    }

    [Fact]
    public void ApplyUpdatableFields_BehaeltEvidenceFramePathBeiTeilUpdate()
    {
        var target = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            EvidenceFramePath = @"C:\frames\s1_markiert.png"
        };
        var source = new TrainingSample { SampleId = "s1", Code = "BCA", Status = TrainingSampleStatus.Approved };

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.Equal(@"C:\frames\s1_markiert.png", target.EvidenceFramePath);
    }

    [Fact]
    public void ApplyUpdatableFields_UebernimmtEvidenceFramePathWennGesetzt()
    {
        var target = new TrainingSample { SampleId = "s1", Code = "BCA" };
        var source = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            EvidenceFramePath = @"C:\frames\s1_markiert.png"
        };

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.Equal(@"C:\frames\s1_markiert.png", target.EvidenceFramePath);
    }
}
