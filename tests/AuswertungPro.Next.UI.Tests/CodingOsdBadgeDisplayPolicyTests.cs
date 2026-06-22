using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdBadgeDisplayPolicyTests
{
    [Fact]
    public void BuildMeterText_formats_osd_meter_badge()
    {
        Assert.Equal("12.35m (OSD)", CodingOsdBadgeDisplayPolicy.BuildMeterText(12.345));
    }
}
