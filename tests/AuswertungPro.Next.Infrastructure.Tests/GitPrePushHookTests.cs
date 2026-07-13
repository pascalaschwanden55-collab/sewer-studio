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
        var command = $"dotnet test tests/{projectName}/{projectName}.csproj -v minimal --no-restore";
        var blockingCommand = $"{Regex.Escape(command)}\\s*\\|\\|\\s*\\{{(?:(?!dotnet test).)*exit 1";

        Assert.Matches(new Regex(blockingCommand, RegexOptions.Singleline), hook);
    }
}
