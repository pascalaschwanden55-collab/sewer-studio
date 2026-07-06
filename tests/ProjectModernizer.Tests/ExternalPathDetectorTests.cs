using Xunit;

public sealed class ExternalPathDetectorTests
{
    [Fact]
    public void IsExternalAbsolutePathIgnoresRelativeAndProjectInternalPaths()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        Directory.CreateDirectory(projectFolder);

        var inside = Path.Combine(projectFolder, "Projektdateien", "logo.png");
        var outside = Path.Combine(temp.Root, "source", "logo.png");

        Assert.False(ExternalPathDetector.IsExternalAbsolutePath(@"Projektdateien\logo.png", projectFolder));
        Assert.False(ExternalPathDetector.IsExternalAbsolutePath(inside, projectFolder));
        Assert.True(ExternalPathDetector.IsExternalAbsolutePath(outside, projectFolder));
    }

    [Fact]
    public void ContainsExternalDrivePathDetectsOnlyPathsOutsideProjectFolder()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        Directory.CreateDirectory(projectFolder);

        var inside = Path.Combine(projectFolder, "Importdateien", "data.mdb");
        var outside = Path.Combine(temp.Root, "source", "data.mdb");

        Assert.False(ExternalPathDetector.ContainsExternalDrivePath($"Quelle={inside}", projectFolder));
        Assert.True(ExternalPathDetector.ContainsExternalDrivePath($"Quelle={outside}", projectFolder));
    }
}
