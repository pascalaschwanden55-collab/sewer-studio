using System.IO;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class KiReleaseGateArchitectureTests
{
    private const string GoldenTestName =
        "AuswertungPro.Next.Pipeline.Tests.SidecarRealVideoIntegrationTests.EchtesVideo_ErfuelltGoldenVertrag";

    [Fact]
    public void Gate_startet_genau_den_Golden_Test_und_schreibt_einen_commitgebundenen_Beleg()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile("scripts", "ki-release-gate.ps1"));

        Assert.Contains($"FullyQualifiedName={GoldenTestName}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("--filter 'Category=Integration'", source, StringComparison.Ordinal);
        Assert.Contains("gate-receipt.json", source, StringComparison.Ordinal);
        Assert.Contains("source_commit", source, StringComparison.Ordinal);
        Assert.Contains(GoldenTestName, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Release_verlangt_den_passenden_Golden_Beleg_und_einen_sauberen_Quellstand()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile("tools", "Publish-SewerStudio.ps1"));

        Assert.Contains("gate-receipt.json", source, StringComparison.Ordinal);
        Assert.Contains("source_commit", source, StringComparison.Ordinal);
        Assert.Contains(GoldenTestName, source, StringComparison.Ordinal);
        Assert.Contains("Quellstand", source, StringComparison.Ordinal);
        Assert.Contains("nicht zum aktuellen Commit", source, StringComparison.Ordinal);
    }
}
