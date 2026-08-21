using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CoverageGateArchitectureTests
{
    [Fact]
    public void Coverage_Grenze_darf_gegenueber_dem_Vergleichscommit_nicht_sinken()
    {
        var script = File.ReadAllText(RepoFile(".github", "scripts", "check-coverage.ps1"));
        var workflow = File.ReadAllText(RepoFile(".github", "workflows", "ci.yml"));

        Assert.Contains("[double]$RatchetToleranz = 0.5", script, StringComparison.Ordinal);
        Assert.Contains("GITHUB_BASE_REF", script, StringComparison.Ordinal);
        Assert.Contains("HEAD^", script, StringComparison.Ordinal);
        Assert.Contains("git show", script, StringComparison.Ordinal);
        Assert.Contains("darf nicht sinken", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
    }
}
