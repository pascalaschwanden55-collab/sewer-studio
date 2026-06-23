using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingStreckenschadenActionApplier
{
    public static bool Apply(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        IReadOnlyList<CodingEvent> codingEvents,
        ICodingSessionService codingSessionService,
        TimeSpan videoTime,
        Func<string, string?> lookupVsaLabel,
        Action<ProtocolEntry> attachAnalyzedFramePhoto)
    {
        ArgumentNullException.ThrowIfNull(codingSessionService);
        ArgumentNullException.ThrowIfNull(lookupVsaLabel);
        ArgumentNullException.ThrowIfNull(attachAnalyzedFramePhoto);

        if (actions.Count == 0)
            return false;

        var openEntries = CodingStreckenschadenActionInputBuilder.BuildOpenEntries(codingEvents);
        var instructions = StreckenschadenActionMapper.MapAll(actions, openEntries);
        if (instructions.Count == 0)
            return false;

        bool anyChanged = false;
        foreach (var instr in instructions)
        {
            switch (instr.Kind)
            {
                case StreckenschadenActionMapper.InstructionKind.CreateOpen:
                {
                    var draft = CodingStreckenschadenEventFactory.CreateOpen(
                        instr.MainCode,
                        lookupVsaLabel(instr.MainCode),
                        instr.StartMeter,
                        videoTime);
                    attachAnalyzedFramePhoto(draft.Entry);
                    var ev = codingSessionService.AddEvent(draft.Entry);
                    ev.MeterAtCapture = instr.StartMeter;
                    ev.AiContext = draft.AiContext;
                    anyChanged = true;
                    break;
                }
                case StreckenschadenActionMapper.InstructionKind.CloseExisting:
                {
                    if (instr.TargetReference is CodingEvent target)
                    {
                        target.Entry.MeterEnd = instr.EndMeter;
                        target.Entry.IsStreckenschaden = true;
                        codingSessionService.UpdateEvent(target.EventId, target.Entry, target.Overlay);
                        anyChanged = true;
                    }
                    break;
                }
            }
        }

        return anyChanged;
    }
}
