using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class HaltungsgrafikExportSizingTests
{
    [Fact]
    public void ChooseSvgHeight_keeps_standard_height_for_normal_protocol()
    {
        Assert.Equal(700, HaltungsgrafikExportSizing.ChooseSvgHeight(26));
    }

    [Fact]
    public void ChooseSvgHeight_increases_height_for_dense_protocol()
    {
        Assert.True(HaltungsgrafikExportSizing.ChooseSvgHeight(41) > 700);
    }

    [Fact]
    public void ChooseSvgHeight_caps_extreme_protocols()
    {
        Assert.Equal(1100, HaltungsgrafikExportSizing.ChooseSvgHeight(200));
    }
}
