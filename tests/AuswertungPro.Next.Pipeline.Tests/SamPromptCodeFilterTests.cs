using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SamPromptCodeFilterTests
{
    [Theory]
    [InlineData("BCC")]
    [InlineData("BCCAY")]
    [InlineData("BCCBY")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    public void StructuralPipeCodes_AreNotUsedAsLocalSamPrompts(string code)
    {
        Assert.False(SamPromptCodeFilter.ShouldUseAsSamPrompt(code));
    }

    [Theory]
    [InlineData("BCA")]
    [InlineData("BCAAA")]
    [InlineData("BAB")]
    [InlineData("root intrusion")]
    public void LocalFindings_AreStillUsedAsSamPrompts(string label)
    {
        Assert.True(SamPromptCodeFilter.ShouldUseAsSamPrompt(label));
    }
}
