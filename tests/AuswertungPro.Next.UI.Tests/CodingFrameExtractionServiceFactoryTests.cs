using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFrameExtractionServiceFactoryTests
{
    [Fact]
    public void Create_returns_frame_extraction_service()
    {
        var service = CodingFrameExtractionServiceFactory.Create();

        Assert.IsType<CodingFrameExtractionService>(service);
    }
}
