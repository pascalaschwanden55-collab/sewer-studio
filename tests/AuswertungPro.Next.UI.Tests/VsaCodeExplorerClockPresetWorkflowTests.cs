using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockPresetWorkflowTests
{
    [Fact]
    public void Resolve_uebernimmt_von_und_bis_aus_gueltigem_tag()
    {
        var result = VsaCodeExplorerClockPresetWorkflow.Resolve("03,09");

        Assert.True(result.ShouldApply);
        Assert.Equal("03", result.ClockVonText);
        Assert.Equal("09", result.ClockBisText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("03")]
    [InlineData("03,09,12")]
    public void Resolve_ignoriert_ungueltige_tags(string? tag)
    {
        var result = VsaCodeExplorerClockPresetWorkflow.Resolve(tag);

        Assert.False(result.ShouldApply);
        Assert.Equal("", result.ClockVonText);
        Assert.Equal("", result.ClockBisText);
    }
}
