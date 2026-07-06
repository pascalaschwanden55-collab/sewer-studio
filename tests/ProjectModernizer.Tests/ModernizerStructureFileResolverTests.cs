using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

public sealed class ModernizerStructureFileResolverTests
{
    [Fact]
    public void ResolveExistingFileResolvesRelativeProjectPath()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        var video = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.1-2", "20250101_06.1-2.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");

        var resolved = ModernizerStructureFileResolver.ResolveExistingFile(
            @"Haltungen_Verteilt\06.1-2\20250101_06.1-2.mp4",
            projectFolder,
            MediaFileTypes.HasVideoExtension);

        Assert.Equal(video, resolved);
    }

    [Fact]
    public void ResolveExistingFileResolvesAbsolutePathWhenTypeMatches()
    {
        using var temp = TempProject.Create();
        var image = Path.Combine(temp.Root, "source", "foto.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        File.WriteAllText(image, "image");

        var resolved = ModernizerStructureFileResolver.ResolveExistingFile(
            image,
            Path.Combine(temp.Root, "project"),
            MediaFileTypes.HasImageExtension);

        Assert.Equal(image, resolved);
    }

    [Fact]
    public void ResolveExistingFileRejectsExistingFileWithWrongType()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        var text = Path.Combine(projectFolder, "Haltungen_Verteilt", "readme.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(text)!);
        File.WriteAllText(text, "text");

        var resolved = ModernizerStructureFileResolver.ResolveExistingFile(
            @"Haltungen_Verteilt\readme.txt",
            projectFolder,
            MediaFileTypes.HasVideoExtension);

        Assert.Null(resolved);
    }
}
