using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryVmNormalizerArchitectureTests
{
    [Fact]
    public void ViewModel_delegiert_Defaultformatierung_und_Streckennormalisierung()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Protocol",
            "ProtocolEntryVM.cs"));

        var applyCode = GetBlock(source, "public void ApplyCodeSelection(", "public void EnsureVsaDefaults()");
        Assert.Contains("VsaParameterMerger.NormalizeAliases(", applyCode);
        Assert.DoesNotContain("NormalizeSecAliases", source);

        var defaults = GetBlock(source, "public void EnsureVsaDefaults()", "public void ApplyStreckenLogik()");
        Assert.Contains("ProtocolEntryInputNormalizer.FormatDouble(MeterStart.Value)", defaults);
        Assert.Contains("ProtocolEntryInputNormalizer.FormatTime(Zeit.Value)", defaults);

        var range = GetBlock(source, "public void ApplyStreckenLogik()", "public void ApplyAiSuggestionToModelAndVm");
        Assert.Contains("ProtocolEntryInputNormalizer.TryNormalizeStrecke(", range);
        Assert.DoesNotContain("char.IsDigit", range);
        Assert.DoesNotContain("ToUpperInvariant", range);

        Assert.DoesNotContain("private static string FormatTime", source);
        Assert.DoesNotContain("ToString(\"0.00\", System.Globalization.CultureInfo.InvariantCulture)", source);
    }

    private static string GetBlock(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{startMarker}' wurde nicht gefunden.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"'{endMarker}' wurde nicht nach '{startMarker}' gefunden.");
        return source[start..end];
    }
}
