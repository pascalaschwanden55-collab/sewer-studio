using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AnalyzerConfigurationTests
{
    [Fact]
    public void Empfohlene_Analyzer_sind_mit_dokumentierter_Baseline_aktiv()
    {
        var props = File.ReadAllText(TestRepoPaths.RepoFile("Directory.Build.props"));
        var baseline = File.ReadAllText(TestRepoPaths.RepoFile(".editorconfig"));

        Assert.Contains("<EnableNETAnalyzers>true</EnableNETAnalyzers>", props, StringComparison.Ordinal);
        Assert.Contains("<AnalysisLevel>latest-recommended</AnalysisLevel>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", props, StringComparison.Ordinal);
        Assert.Contains("Analyzer-Einstiegsbaseline", baseline, StringComparison.Ordinal);
        Assert.Contains("severity = suggestion", baseline, StringComparison.Ordinal);
        Assert.DoesNotContain("severity = none", baseline, StringComparison.Ordinal);
    }
}
