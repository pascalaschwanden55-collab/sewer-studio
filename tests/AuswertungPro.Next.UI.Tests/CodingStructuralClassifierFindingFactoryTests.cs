using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStructuralClassifierFindingFactoryTests
{
    [Fact]
    public void Create_builds_structural_live_frame_finding_with_code_hint()
    {
        var finding = CodingStructuralClassifierFindingFactory.Create(
            code: "BCA",
            label: "Anschluss");

        Assert.Equal("Anschluss", finding.Label);
        Assert.Equal(3, finding.Severity);
        Assert.Null(finding.PositionClock);
        Assert.Null(finding.ExtentPercent);
        Assert.Equal("BCA", finding.VsaCodeHint);
        Assert.Null(finding.BboxX1);
        Assert.Null(finding.BboxY1);
        Assert.Null(finding.BboxX2);
        Assert.Null(finding.BboxY2);
    }
}
