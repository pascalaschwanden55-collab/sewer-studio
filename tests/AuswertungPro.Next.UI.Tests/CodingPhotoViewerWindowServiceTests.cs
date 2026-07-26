using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoViewerWindowServiceTests
{
    [Fact]
    public void BuildTitle_uses_code_and_meter()
    {
        var codingEvent = new CodingEvent
        {
            MeterAtCapture = 12.345,
            Entry = new ProtocolEntry { Code = "BAA" }
        };

        var title = CodingPhotoViewerWindowService.BuildTitle(codingEvent);

        Assert.Equal("Fotos - BAA @ 12.35m", title);
    }

    [Fact]
    public void Factory_creates_viewer_service()
    {
        var service = CodingPhotoViewerWindowServiceFactory.Create();

        Assert.NotNull(service);
    }
}
