using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public class RuleBasedPlausibilityTests
{
    private static readonly IReadOnlySet<string> KnownCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BAA",
            "BBA",
            "BCD",
            "BCE",
            "BDC"
        };

    private readonly RuleBasedAiSuggestionPlausibilityService _sut = new(KnownCodes);

    [Fact]
    public void NullCode_ReturnsUnchanged()
    {
        var input = new AiSuggestionResult(null, 0.8, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("crack"));
        Assert.Null(result.SuggestedCode);
        Assert.Equal(0.8, result.Confidence);
    }

    [Fact]
    public void EmptyCode_ReturnsUnchanged()
    {
        var input = new AiSuggestionResult("  ", 0.8, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("crack"));
        Assert.Equal("  ", result.SuggestedCode);
        Assert.Equal(0.8, result.Confidence);
    }

    [Fact]
    public void KnownCode_ReturnsUnchanged()
    {
        var input = new AiSuggestionResult("BAA", 0.9, "Laengsriss", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("Laengsriss"));
        Assert.Equal("BAA", result.SuggestedCode);
        Assert.Equal(0.9, result.Confidence);
    }

    [Fact]
    public void DefaultConstructor_DoesNotUseStaticCatalogFallback()
    {
        var sut = new RuleBasedAiSuggestionPlausibilityService();
        var input = new AiSuggestionResult("BAA", 0.9, "Laengsriss", null, null);

        var result = sut.ApplyChecks(input, new ObservationContext("Laengsriss"));

        Assert.Equal("BAA", result.SuggestedCode);
        Assert.Equal(0.5, result.Confidence, 2);
        Assert.Contains(result.Warnings!, w => w.Contains("PL02"));
    }

    [Fact]
    public void InvalidFormat_NullifiesCodeAndPenalizes()
    {
        var input = new AiSuggestionResult("CRACK", 0.8, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("crack"));
        Assert.Null(result.SuggestedCode);
        Assert.True(result.Confidence < 0.8);
        Assert.Contains(result.Warnings!, w => w.Contains("PL01"));
    }

    [Fact]
    public void UnknownCatalogCode_ReducesConfidence()
    {
        var input = new AiSuggestionResult("BHZ", 0.9, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("unknown"));
        Assert.Equal("BHZ", result.SuggestedCode);
        Assert.Equal(0.5, result.Confidence, 2);
        Assert.Contains(result.Warnings!, w => w.Contains("PL02"));
    }

    [Fact]
    public void ObservationMismatch_CrackWithNonCrackCode_AddsWarning()
    {
        var input = new AiSuggestionResult("BBA", 0.85, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("Laengsriss in der Sohle"));
        Assert.Equal("BBA", result.SuggestedCode);
        Assert.Equal(0.85, result.Confidence);
        Assert.Contains(result.Warnings!, w => w.Contains("PL03"));
    }

    [Fact]
    public void ObservationMismatch_DeformationWithNonDeformCode_AddsWarning()
    {
        var input = new AiSuggestionResult("BAA", 0.85, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("Verformung im Scheitel"));
        Assert.Equal("BAA", result.SuggestedCode);
        Assert.Equal(0.85, result.Confidence);
        Assert.Contains(result.Warnings!, w => w.Contains("PL03"));
    }

    [Fact]
    public void ExistingWarnings_ArePreserved()
    {
        var input = new AiSuggestionResult("BAA", 0.9, "test", null,
            new[] { "prior-warning" });
        var result = _sut.ApplyChecks(input, new ObservationContext("Riss"));
        Assert.Contains(result.Warnings!, w => w == "prior-warning");
    }

    [Fact]
    public void LowercaseCode_IsNormalized()
    {
        var input = new AiSuggestionResult("baa", 0.9, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("Riss"));
        Assert.Equal("BAA", result.SuggestedCode);
    }

    [Fact]
    public void ConfidenceNeverNegative()
    {
        var input = new AiSuggestionResult("INVALID", 0.1, "test", null, null);
        var result = _sut.ApplyChecks(input, new ObservationContext("test"));
        Assert.True(result.Confidence >= 0.0);
    }

    [Fact]
    public void CatalogCode_NonVsaFormat_PassesWithSoftWarning()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ZXZ", "BAA" };
        var sut = new RuleBasedAiSuggestionPlausibilityService(allowed);
        var input = new AiSuggestionResult("ZXZ", 0.85, "test", null, null);

        var result = sut.ApplyChecks(input, new ObservationContext("Grundlageninfo"));

        Assert.Equal("ZXZ", result.SuggestedCode);
        Assert.True(result.Confidence >= 0.65, "Confidence should only have small penalty");
        Assert.Contains(result.Warnings!, w => w.Contains("PL01") && w.Contains("im Katalog bekannt"));
    }

    [Fact]
    public void CatalogCode_VsaFormat_PassesWithoutWarning()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BAA", "BBA" };
        var sut = new RuleBasedAiSuggestionPlausibilityService(allowed);
        var input = new AiSuggestionResult("BAA", 0.9, "Laengsriss", null, null);

        var result = sut.ApplyChecks(input, new ObservationContext("Riss"));

        Assert.Equal("BAA", result.SuggestedCode);
        Assert.Equal(0.9, result.Confidence);
    }

    [Fact]
    public void CatalogCode_IkasFullCode_PassesWithoutFormatPenalty()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BAGA", "BABAC", "DCA" };
        var sut = new RuleBasedAiSuggestionPlausibilityService(allowed);
        var input = new AiSuggestionResult("BAGA", 0.9, "Anschluss einragend", null, null);

        var result = sut.ApplyChecks(input, new ObservationContext("Anschluss einragend"));

        Assert.Equal("BAGA", result.SuggestedCode);
        Assert.Equal(0.9, result.Confidence);
        Assert.DoesNotContain(result.Warnings ?? Array.Empty<string>(), w => w.Contains("PL01"));
    }

    [Fact]
    public void CatalogAware_UnknownCode_ReducesConfidence()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BAA" };
        var sut = new RuleBasedAiSuggestionPlausibilityService(allowed);
        var input = new AiSuggestionResult("BHZ", 0.9, "test", null, null);

        var result = sut.ApplyChecks(input, new ObservationContext("test"));

        Assert.Equal("BHZ", result.SuggestedCode);
        Assert.Equal(0.5, result.Confidence, 2);
        Assert.Contains(result.Warnings!, w => w.Contains("PL02"));
    }
}
