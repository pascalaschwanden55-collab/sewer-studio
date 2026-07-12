using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class WinCanReadOnlyArchitectureTests
{
    [Fact]
    public void WinCanDatabase_IsOpenedReadOnly()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "WinCan", "WinCanDbImportService.cs"));

        Assert.Contains("Mode = SqliteOpenMode.ReadOnly", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SqliteConnection($\"Data Source={dbPath};\")", source, StringComparison.Ordinal);
    }
}
