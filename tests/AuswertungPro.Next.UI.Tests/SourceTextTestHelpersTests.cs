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
}
