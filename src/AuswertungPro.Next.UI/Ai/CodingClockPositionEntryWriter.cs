using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingClockPositionEntryWriter
{
    public static void ApplyToEntry(
        ProtocolEntry entry,
        string code,
        SegmentedFinding segmented,
        double imageWidth,
        double imageHeight,
        PipeCalibration? calibration,
        QuantificationGate.ManifestQuantRule manifestRule)
    {
        if (segmented.Mask.Bbox is not { Count: >= 4 } || imageWidth <= 0 || imageHeight <= 0)
            return;

        if (!manifestRule.AllowClock)
            return;

        var pipeCenterX = calibration?.PipeCenter.X ?? 0.5;
        var pipeCenterY = calibration?.PipeCenter.Y ?? 0.5;
        var isCalibrated = calibration is { IsCalibrated: true };

        var box = new ClockPositionResolver.NormBox(
            segmented.Mask.Bbox[0] / imageWidth,
            segmented.Mask.Bbox[1] / imageHeight,
            segmented.Mask.Bbox[2] / imageWidth,
            segmented.Mask.Bbox[3] / imageHeight);

        var span = ClockPositionResolver.Resolve(box, pipeCenterX, pipeCenterY, isCalibrated, code);
        var from = ClockPositionResolver.FormatFrom(span);
        var to = ClockPositionResolver.FormatTo(span);

        if (from == null)
        {
            entry.CodeMeta?.Parameters.Remove("vsa.uhr.von");
            entry.CodeMeta?.Parameters.Remove("vsa.uhr.bis");
            return;
        }

        entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        entry.CodeMeta.Parameters["vsa.uhr.von"] = from;
        if (to != null)
            entry.CodeMeta.Parameters["vsa.uhr.bis"] = to;
        else
            entry.CodeMeta.Parameters.Remove("vsa.uhr.bis");
    }
}
