using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectAgentInstructionsTests
{
    [Fact]
    public void Agenteneinstieg_verweist_auf_aktuelle_Architektur()
    {
        var instructions = File.ReadAllText(TestRepoPaths.RepoFile("AGENTS.md"));

        Assert.Contains("CLAUDE.md", instructions, StringComparison.Ordinal);
        Assert.Contains(".NET 10", instructions, StringComparison.Ordinal);
        Assert.Contains("Python-Sidecar", instructions, StringComparison.Ordinal);
        Assert.Contains("QGIS", instructions, StringComparison.Ordinal);
        Assert.Contains("Keine God-Classes", instructions, StringComparison.Ordinal);
        Assert.Contains("AuswertungPro.Dev.slnf", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("z.B. net8.0", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Tests: `dotnet test` (falls vorhanden)", instructions, StringComparison.Ordinal);
    }
}
