using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolProjectFolderResolverTests
{
    [Fact]
    public void Resolve_returns_project_directory_for_project_file_path()
    {
        var result = CodingProtocolProjectFolderResolver.Resolve(@"C:\Projects\Sewer\project.aproj");

        Assert.Equal(@"C:\Projects\Sewer", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_returns_null_for_missing_project_path(string? projectPath)
    {
        var result = CodingProtocolProjectFolderResolver.Resolve(projectPath);

        Assert.Null(result);
    }
}
