using Xunit;

public sealed class ModernizerSourceIndexBuilderTests
{
    [Fact]
    public void BuildSourceVideoIndexUsesHoldingNameBeforeFirstUnderscore()
    {
        using var temp = TempProject.Create();
        var sourceFolder = Path.Combine(temp.Root, "source");
        Directory.CreateDirectory(sourceFolder);

        var suffixed = Path.Combine(sourceFolder, "06.24341-35625_GUID.mp4");
        var plain = Path.Combine(sourceFolder, "07.111-222.avi");
        var ignored = Path.Combine(sourceFolder, "photo.jpg");
        File.WriteAllText(suffixed, "video");
        File.WriteAllText(plain, "video");
        File.WriteAllText(ignored, "image");

        var index = ModernizerSourceIndexBuilder.BuildSourceVideoIndex(sourceFolder);

        Assert.True(index.TryGetValue("06.24341-35625", out var suffixedMatches));
        Assert.Contains(suffixed, suffixedMatches);
        Assert.True(index.TryGetValue("07.111-222", out var plainMatches));
        Assert.Contains(plain, plainMatches);
        Assert.DoesNotContain("photo", index.Keys);
    }

    [Fact]
    public void BuildExternalFileIndexIncludesKnownMediaAndPdfFromSourceAndImports()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        var imports = Path.Combine(projectFolder, ModernizerLegacyFolders.Imports);
        var sourceFolder = Path.Combine(temp.Root, "source");
        Directory.CreateDirectory(imports);
        Directory.CreateDirectory(sourceFolder);

        var pdf = Path.Combine(imports, "old.pdf");
        var video = Path.Combine(sourceFolder, "clip.mp4");
        var image = Path.Combine(sourceFolder, "frame.jpg");
        var ignored = Path.Combine(sourceFolder, "notes.txt");
        File.WriteAllText(pdf, "pdf");
        File.WriteAllText(video, "video");
        File.WriteAllText(image, "image");
        File.WriteAllText(ignored, "text");

        var index = ModernizerSourceIndexBuilder.BuildExternalFileIndex(projectFolder, sourceFolder);

        Assert.Contains(pdf, index["old.pdf"]);
        Assert.Contains(video, index["clip.mp4"]);
        Assert.Contains(image, index["frame.jpg"]);
        Assert.False(index.ContainsKey("notes.txt"));
    }
}
