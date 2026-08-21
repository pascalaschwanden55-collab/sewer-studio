using System.Text.RegularExpressions;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GitPrePushHookTests
{
    private static readonly string[] RequiredProjectNames =
    [
        "AuswertungPro.Next.Infrastructure.Tests",
        "AuswertungPro.Next.Pipeline.Tests",
        "AuswertungPro.Next.UI.Tests",
        "ProjectModernizer.Tests"
    ];

    public static TheoryData<string> RequiredTestProjects => new()
    {
        RequiredProjectNames[0],
        RequiredProjectNames[1],
        RequiredProjectNames[2],
        RequiredProjectNames[3]
    };

    [Theory]
    [MemberData(nameof(RequiredTestProjects))]
    public void PrePushHook_fuehrtAlleTestprojekteAusUndBlockiertBeiFehler(string projectName)
    {
        var hook = File.ReadAllText(RepoFile(".githooks", "pre-push"));
        var projectPath = $"tests/{projectName}/{projectName}.csproj";
        var blockingCommand = $"run_gate\\s+{Regex.Escape(projectPath)}\\s+[^\\r\\n]+\\|\\|\\s+exit 1";

        Assert.Matches(new Regex(blockingCommand, RegexOptions.Singleline), hook);
    }

    [Fact]
    public void Installationsskript_aktiviert_den_versionierten_Hook_ohne_zweite_Kopie()
    {
        var installer = File.ReadAllText(RepoFile("tools", "git-hooks", "Install-PrePushHook.ps1"));

        Assert.Contains("core.hooksPath", installer, StringComparison.Ordinal);
        Assert.Contains(".githooks", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", installer, StringComparison.Ordinal);
        var obsoleteCopy = Path.Combine(RepoRoot(), "tools", "git-hooks", "pre-push");
        Assert.False(File.Exists(obsoleteCopy));
    }

    [Fact]
    public void Dokumentation_nennt_alle_vier_Testprojekte_und_die_bewussten_Grenzen()
    {
        var documentation = File.ReadAllText(RepoFile("docs", "ENTWICKLUNGS-GATE.md"));

        foreach (var projectName in RequiredProjectNames)
        {
            Assert.Contains(projectName, documentation, StringComparison.Ordinal);
        }

        Assert.Contains("Sidecar", documentation, StringComparison.Ordinal);
        Assert.Contains("QGIS", documentation, StringComparison.Ordinal);
    }
}
