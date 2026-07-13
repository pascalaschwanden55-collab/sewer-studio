using System.Text.RegularExpressions;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GitPrePushHookTests
{
    public static TheoryData<string> RequiredTestProjects => new()
    {
        "AuswertungPro.Next.Infrastructure.Tests",
        "AuswertungPro.Next.Pipeline.Tests",
        "AuswertungPro.Next.UI.Tests"
    };

    [Theory]
    [MemberData(nameof(RequiredTestProjects))]
    public void PrePushHook_fuehrtAlleTestprojekteAusUndBlockiertBeiFehler(string projectName)
    {
        var hook = File.ReadAllText(RepoFile("tools", "git-hooks", "pre-push"));
        var command = $"dotnet test tests/{projectName}/{projectName}.csproj -v minimal --no-restore --filter \"Category!=Integration&Category!=Endurance\"";
        var blockingCommand = $"{Regex.Escape(command)}\\s*\\|\\|\\s*\\{{(?:(?!dotnet test).)*exit 1";

        Assert.Matches(new Regex(blockingCommand, RegexOptions.Singleline), hook);
    }

    [Fact]
    public void PrePushHook_laesst_maschinengebundene_und_Nachtlauftests_aus()
    {
        var hook = File.ReadAllText(RepoFile("tools", "git-hooks", "pre-push"));

        Assert.Equal(3, Regex.Matches(hook, "Category!=Integration&Category!=Endurance").Count);
    }
}
