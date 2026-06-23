using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProjectFolderResolverTests
{
    [Fact]
    public void ResolveNullable_returns_project_directory_for_project_file_path()
    {
        var result = CodingProjectFolderResolver.ResolveNullable(@"C:\Projects\Sewer\project.aproj");

        Assert.Equal(@"C:\Projects\Sewer", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveNullable_returns_null_for_missing_project_path(string? projectPath)
    {
        var result = CodingProjectFolderResolver.ResolveNullable(projectPath);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveOrEmpty_returns_empty_string_for_missing_project_path(string? projectPath)
    {
        var result = CodingProjectFolderResolver.ResolveOrEmpty(projectPath);

        Assert.Equal(string.Empty, result);
    }
}
