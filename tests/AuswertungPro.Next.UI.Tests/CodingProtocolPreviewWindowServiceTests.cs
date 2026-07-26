using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPreviewWindowServiceTests
{
    [Fact]
    public void Factory_creates_preview_window_service()
    {
        var service = CodingProtocolPreviewWindowServiceFactory.Create();

        Assert.NotNull(service);
    }
}
