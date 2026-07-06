using Xunit;

public sealed class ModernizeOptionsTests
{
    [Fact]
    public void ParseRecognizesAllOptions()
    {
        var options = ModernizeOptions.Parse(new[]
        {
            "--project-folder", "P",
            "--project-file", "F",
            "--source-folder", "S",
            "--dry-run",
            "--flatten-only"
        });

        Assert.NotNull(options);
        Assert.Equal("P", options!.ProjectFolder);
        Assert.Equal("F", options.ProjectFile);
        Assert.Equal("S", options.SourceFolder);
        Assert.True(options.DryRun);
        Assert.True(options.FlattenOnly);
    }

    [Fact]
    public void ParseRejectsMissingProjectFolder()
    {
        Assert.Null(ModernizeOptions.Parse(Array.Empty<string>()));
        Assert.Null(ModernizeOptions.Parse(new[] { "--source-folder", "S" }));
    }

    [Fact]
    public void ParseRejectsUnknownOrIncompleteOption()
    {
        Assert.Null(ModernizeOptions.Parse(new[] { "--unknown" }));
        Assert.Null(ModernizeOptions.Parse(new[] { "--project-folder" }));
    }
}
