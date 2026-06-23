using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingSessionAppenderTests
{
    [Fact]
    public void Append_attaches_photo_before_add_event_and_applies_ai_context_and_overlay()
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry { Code = "BAB" };
        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = "BAB",
            Confidence = 0.87,
            Reason = "Riss"
        };
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Rectangle };
        var draft = new CodingLiveFindingEventDraft(entry, aiContext, overlay);

        var codingEvent = CodingLiveFindingSessionAppender.Append(
            draft,
            attachAnalyzedFramePhoto: attachedEntry =>
            {
                calls.Add("attach");
                Assert.Same(entry, attachedEntry);
            },
            addEvent: addedEntry =>
            {
                calls.Add("add");
                Assert.Same(entry, addedEntry);
                return new CodingEvent { Entry = addedEntry };
            });

        Assert.Equal(["attach", "add"], calls);
        Assert.Same(aiContext, codingEvent.AiContext);
        Assert.Same(overlay, codingEvent.Overlay);
        Assert.Same(entry, codingEvent.Entry);
    }
}
