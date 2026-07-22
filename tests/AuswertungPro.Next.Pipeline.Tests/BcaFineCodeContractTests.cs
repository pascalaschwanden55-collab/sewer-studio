using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BcaFineCodeContractTests
{
    [Fact]
    public void Uncertain_ist_leer_und_markiert_unsicher()
    {
        var s = BcaFineCodeSuggestion.Uncertain;

        Assert.True(s.IsUncertain);
        Assert.Empty(s.Candidates);
    }

    [Fact]
    public void Suggestion_haelt_kandidaten_in_uebergebener_reihenfolge()
    {
        var s = new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate("BCAAA", 0.7), new BcaFineCodeCandidate("BCAEA", 0.2) },
            IsUncertain: false);

        Assert.False(s.IsUncertain);
        Assert.Equal("BCAAA", s.Candidates[0].VsaCode);
        Assert.Equal(0.2, s.Candidates[1].Confidence);
    }
}
