using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingMultiModelEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext,
    OverlayGeometry? Overlay);

public static class CodingMultiModelEventFactory
{
    public static CodingMultiModelEventDraft Create(
        string code,
        string? officialLabel,
        SegmentedFinding segmented,
        double meter,
        TimeSpan videoTime,
        double dinoConfidence,
        double compositeConfidence,
        double imageWidth,
        double imageHeight,
        bool meterFromOsd,
        PipeCalibration? calibration,
        QuantificationGate.ManifestQuantRule manifestRule,
        EvidenceVector? evidence = null)
    {
        var quant = segmented.Quant;
        var mask = segmented.Mask;

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = officialLabel ?? quant.Label,
            MeterStart = meter,
            Zeit = videoTime
        };

        if (!meterFromOsd)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.meter.quelle"] = "geschaetzt";
        }

        QuantificationCodeMetaWriter.Apply(entry, code, quant, manifestRule);
        if (segmented.Origin is { } origin)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["ai.detector.source"] = origin.SourceName;
            if (origin.DevelopmentCandidate)
                entry.CodeMeta.Parameters["ai.detector.purpose"] = "development_candidate";
            if (origin.HasYolo)
            {
                entry.CodeMeta.Parameters["ai.detector.sha256"] = origin.YoloArtifactSha256!;
                entry.CodeMeta.Parameters["ai.code.detail"] = "Hauptgruppe";
            }
        }
        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            code,
            segmented,
            imageWidth,
            imageHeight,
            calibration,
            manifestRule);

        var aiContext = new CodingEventAiContext
        {
            ObservationHasTechnicalFailure = segmented.Origin?.HasTechnicalFailure == true,
            SuggestedCode = code,
            SuggestedByModelId = segmented.Origin?.HasYolo == true ? segmented.Origin.YoloModelName : null,
            SuggestedByModelSha256 = segmented.Origin?.HasYolo == true ? segmented.Origin.YoloArtifactSha256 : null,
            Confidence = compositeConfidence,
            Reason = segmented.Origin is { } source
                ? $"{officialLabel ?? quant.Label} ({DescribeEvidence(source)})"
                : $"{quant.Label} (DINO {dinoConfidence:P0})",
            Evidence = CodingEventEvidenceMapper.ToSnapshot(
                evidence ?? (segmented.Origin is { } sourceEvidence
                    ? new EvidenceVector(YoloConf: sourceEvidence.YoloConfidence,
                        DinoConf: sourceEvidence.DinoConfidence, SamMaskStability: quant.Confidence,
                        DamageCategory: code)
                    : new EvidenceVector(
                    DinoConf: dinoConfidence,
                    SamMaskStability: quant.Confidence,
                    PlausibilityScore: officialLabel != null ? 0.8 : 0.4,
                    DamageCategory: code))),
            SamMaskRle = mask.MaskRle,
            SamMaskImageWidth = (int)Math.Round(imageWidth),
            SamMaskImageHeight = (int)Math.Round(imageHeight),
            Decision = CodingUserDecision.Ignored
        };

        return new CodingMultiModelEventDraft(
            entry,
            aiContext,
            BuildRectangleOverlay(mask, imageWidth, imageHeight));
    }

    private static string DescribeEvidence(CodingLocalizedDetection origin) => string.Join(" + ",
        new[]
        {
            origin.YoloConfidence.HasValue ? $"YOLO {origin.YoloConfidence.Value:P0}" : null,
            origin.DinoConfidence.HasValue ? $"DINO {origin.DinoConfidence.Value:P0}" : null
        }.Where(text => text is not null));

    private static OverlayGeometry? BuildRectangleOverlay(SamMaskResult mask, double imageWidth, double imageHeight)
    {
        if (mask.Bbox is not { Count: >= 4 } || imageWidth <= 0 || imageHeight <= 0)
            return null;

        var x1 = Math.Clamp(mask.Bbox[0] / imageWidth, 0, 1);
        var y1 = Math.Clamp(mask.Bbox[1] / imageHeight, 0, 1);
        var x2 = Math.Clamp(mask.Bbox[2] / imageWidth, 0, 1);
        var y2 = Math.Clamp(mask.Bbox[3] / imageHeight, 0, 1);

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points =
            [
                new NormalizedPoint(x1, y1),
                new NormalizedPoint(x2, y1),
                new NormalizedPoint(x2, y2),
                new NormalizedPoint(x1, y2)
            ]
        };
    }
}
