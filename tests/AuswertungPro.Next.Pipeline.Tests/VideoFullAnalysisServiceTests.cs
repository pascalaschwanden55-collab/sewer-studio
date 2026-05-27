using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VideoFullAnalysisServiceTests
{
    public VideoFullAnalysisServiceTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Theory]
    [InlineData("Wurzeleinwuchs", "BBA")]
    [InlineData("root intrusion", "BBA")]
    [InlineData("Inkrustation verkalkt", "BBB")]
    [InlineData("attached deposit", "BBB")]
    public void BuildFindingKey_UsesCentralVsaResolver(string label, string expectedCode)
    {
        var finding = new EnhancedFinding(
            Label: label,
            VsaCodeHint: null,
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: null,
            BboxY1: null,
            BboxX2: null,
            BboxY2: null,
            Notes: null);

        var method = typeof(VideoFullAnalysisService).GetMethod(
            "BuildFindingKey",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var key = Assert.IsType<string>(method.Invoke(null, new object[] { finding }));
        Assert.Equal(expectedCode, key);
    }
}
