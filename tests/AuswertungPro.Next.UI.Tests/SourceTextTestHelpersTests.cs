using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextTestHelpersTests
{
    [Fact]
    public void ExtractMethodBody_supports_expression_bodied_methods()
    {
        const string source = """
            internal sealed class Example
            {
                private bool CanRun() => true;
            }
            """;

        var body = SourceTextTestHelpers.ExtractMethodBody(source, "private bool CanRun()");

        Assert.Equal("private bool CanRun() => true;", body.Trim());
    }

    [Fact]
    public void ExtractMethod_supports_block_bodied_methods()
    {
        const string source = """
            internal sealed class Example
            {
                private void Run()
                {
                    DoWork();
                }
            }
            """;

        var body = SourceTextTestHelpers.ExtractMethod(source, "private void Run()");

        Assert.Contains("DoWork();", body);
    }

    [Fact]
    public void RepoFile_combines_repository_root_with_relative_segments()
    {
        var path = SourceTextTestHelpers.RepoFile(
            "tests",
            "AuswertungPro.Next.UI.Tests",
            "SourceTextTestHelpers.cs");

        Assert.True(File.Exists(path), path);
    }
}
