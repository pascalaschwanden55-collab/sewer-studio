using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelFrameAnalysisMapperTests
{
    public MultiModelFrameAnalysisMapperTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public void Map_NichtRelevantesErgebnis_BleibtLeer()
    {
        var result = CreateResult(isRelevant: false, masks: []);

        var analysis = MultiModelFrameAnalysisMapper.Map(result, pipeDiameterMm: 300);

        Assert.Empty(analysis.Findings);
        Assert.Equal(AnalysisOutcome.NoFinding, analysis.Outcome);
    }

    [Fact]
    public void Map_UebernimmtGrunddatenUndBegrenztBoxAufBildbereich()
    {
        var result = CreateResult(
            isRelevant: true,
            masks:
            [
                CreateMask(
                    label: "Seitlicher Anschluss",
                    bbox: [-20, 120, 700, 600])
            ]);

        var analysis = MultiModelFrameAnalysisMapper.Map(result, pipeDiameterMm: 400);

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal(16.4, analysis.Meter);
        Assert.Equal(400, analysis.PipeDiameterMm);
        Assert.Equal(AnalysisOutcome.Ok, analysis.Outcome);
        Assert.Equal("BCA", finding.VsaCodeHint);
        Assert.Equal(0, finding.BboxX1);
        Assert.Equal(0.25, finding.BboxY1);
        Assert.Equal(1, finding.BboxX2);
        Assert.Equal(1, finding.BboxY2);
    }

    [Fact]
    public void Map_UngueltigeBox_LaesstKoordinatenLeer()
    {
        var result = CreateResult(
            isRelevant: true,
            masks: [CreateMask("Wurzeleinwuchs", bbox: [10, 20, 30])]);

        var finding = Assert.Single(MultiModelFrameAnalysisMapper.Map(result, 300).Findings);

        Assert.Null(finding.BboxX1);
        Assert.Null(finding.BboxY1);
        Assert.Null(finding.BboxX2);
        Assert.Null(finding.BboxY2);
    }

    [Fact]
    public void Map_LeereBeschriftung_ErzeugtKeinenBefund()
    {
        var result = CreateResult(
            isRelevant: true,
            masks: [CreateMask("   ", bbox: [10, 20, 30, 40])]);

        var analysis = MultiModelFrameAnalysisMapper.Map(result, 300);

        Assert.Empty(analysis.Findings);
        Assert.Equal(AnalysisOutcome.NoFinding, analysis.Outcome);
    }

    [Fact]
    public void Map_MehrereMasken_BewahrtReihenfolgeUndQuantifizierung()
    {
        var result = CreateResult(
            isRelevant: true,
            masks:
            [
                CreateMask("Seitlicher Anschluss", bbox: [10, 20, 30, 40]),
                CreateMask("root intrusion", bbox: [420, 160, 580, 320])
            ]);

        var findings = MultiModelFrameAnalysisMapper.Map(result, pipeDiameterMm: 400).Findings;

        Assert.Equal(2, findings.Count);
        Assert.Equal("Seitlicher Anschluss", findings[0].Label);
        Assert.Equal("root intrusion", findings[1].Label);
        Assert.Equal("BBA", findings[1].VsaCodeHint);
        Assert.Equal("3:00", findings[1].PositionClock);
        Assert.Equal(143, findings[1].HeightMm);
        Assert.Equal(143, findings[1].WidthMm);
        Assert.Equal(11, findings[1].ExtentPercent);
        Assert.Equal(3, findings[1].CrossSectionReductionPercent);
        Assert.Equal(36, findings[1].IntrusionPercent);
        Assert.Null(findings[1].DiameterReductionMm);
        Assert.Null(findings[1].Notes);
    }

    [Fact]
    public void Map_UngueltigeBildgroesse_LaesstUhrUndBoxLeer()
    {
        var result = CreateResult(
            isRelevant: true,
            masks: [CreateMask("Wurzeleinwuchs", bbox: [10, 20, 30, 40])]) with
        {
            ImageWidth = 0,
            ImageHeight = 0
        };

        var finding = Assert.Single(MultiModelFrameAnalysisMapper.Map(result, 300).Findings);

        Assert.Null(finding.PositionClock);
        Assert.Null(finding.BboxX1);
        Assert.Null(finding.BboxY1);
        Assert.Null(finding.BboxX2);
        Assert.Null(finding.BboxY2);
    }

    private static MultiModelFrameResult CreateResult(
        bool isRelevant,
        IReadOnlyList<SamMaskResult> masks)
        => new(
            TimestampSec: 12.0,
            Meter: 16.4,
            IsRelevant: isRelevant,
            DinoDetections: Array.Empty<DinoDetectionDto>(),
            SamMasks: masks,
            ImageWidth: 640,
            ImageHeight: 480,
            YoloTimeMs: 5,
            DinoTimeMs: 7,
            SamTimeMs: 9);

    private static SamMaskResult CreateMask(string label, IReadOnlyList<double> bbox)
        => new(
            Label: label,
            Confidence: 0.91,
            Bbox: bbox,
            MaskRle: "",
            MaskAreaPixels: 5000,
            ImageAreaPixels: 640 * 480,
            HeightPixels: 160,
            WidthPixels: 160,
            CentroidX: 500,
            CentroidY: 240);
}
