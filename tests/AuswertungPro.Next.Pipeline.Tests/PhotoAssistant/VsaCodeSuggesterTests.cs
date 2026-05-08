using AuswertungPro.Next.Application.Ai.PhotoAssistant;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests.PhotoAssistant;

[Trait("Category", "PhotoAssistant")]
[Trait("Category", "Unit")]
public class VsaCodeSuggesterTests
{
    [Fact]
    public void Deformation7Prozent_LiefertBaa2()
    {
        var sug = VsaCodeSuggester.ForDeformation(7);
        Assert.Equal("BAA 2", sug.Code);
    }

    [Fact]
    public void BendAngle25Grad_LiefertBaj3()
    {
        var sug = VsaCodeSuggester.ForBendAngle(25);
        Assert.Equal("BAJ 3", sug.Code);
    }

    [Fact]
    public void LateralAt3Uhr50Prozent_LiefertBcaMitParametern()
    {
        var sug = VsaCodeSuggester.ForLateral(hour: 3, lateralDnPercent: 50, lateralAngleDegrees: 90, dnMm: 300);
        Assert.Equal("BCA", sug.Code);
        Assert.Contains("150", sug.Description);  // 300 * 50% = 150 mm
        Assert.Contains("3h", sug.Description);
    }
}
