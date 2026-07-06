using Xunit;

public sealed class ModernizerStructureFileMoverTests
{
    [Fact]
    public void MoveOrCopyStructureFileCopiesExternalSource()
    {
        using var temp = TempProject.Create();
        var projectFolder = Path.Combine(temp.Root, "project");
        var moveRoot = Path.Combine(projectFolder, "Haltungen_Verteilt");
        var source = Path.Combine(temp.Root, "source", "video.mp4");
        var target = Path.Combine(moveRoot, "06.1-2", "20250101_06.1-2.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "video");

        var report = new ModernizeReport();
        var result = ModernizerStructureFileMover.MoveOrCopyStructureFile(
            source,
            target,
            moveRoot,
            dryRun: false,
            report,
            StructureMoveKind.FlatMedia);

        Assert.Equal(target, result);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(target));
        Assert.Equal(1, report.FlattenedFiles);
    }

    [Fact]
    public void MoveOrCopyStructureFileCopiesSourceInsideMoveRoot()
    {
        using var temp = TempProject.Create();
        var moveRoot = Path.Combine(temp.Root, "project", "Haltungen_Verteilt");
        var source = Path.Combine(moveRoot, "06.1-2", ModernizerLegacyFolders.HoldingVideo, "video.mp4");
        var target = Path.Combine(moveRoot, "06.1-2", "20250101_06.1-2.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "video");

        var report = new ModernizeReport();
        var result = ModernizerStructureFileMover.MoveOrCopyStructureFile(
            source,
            target,
            moveRoot,
            dryRun: false,
            report,
            StructureMoveKind.FlatMedia);

        Assert.Equal(target, result);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(target));
        Assert.Equal("video", File.ReadAllText(source));
        Assert.Equal("video", File.ReadAllText(target));
        Assert.Equal(1, report.FlattenedFiles);
    }

    [Fact]
    public void MoveOrCopyStructureFileReusesIdenticalTargetWithoutDeletingSource()
    {
        using var temp = TempProject.Create();
        var moveRoot = Path.Combine(temp.Root, "project", "Haltungen_Verteilt");
        var source = Path.Combine(moveRoot, "06.1-2", ModernizerLegacyFolders.HoldingVideo, "video.mp4");
        var target = Path.Combine(moveRoot, "06.1-2", "20250101_06.1-2.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(source, "same-size");
        File.WriteAllText(target, "same-size");

        var report = new ModernizeReport();
        var result = ModernizerStructureFileMover.MoveOrCopyStructureFile(
            source,
            target,
            moveRoot,
            dryRun: false,
            report,
            StructureMoveKind.FlatMedia);

        Assert.Equal(target, result);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(target));
        Assert.Equal(1, report.ReusedFiles);
        Assert.Equal(1, report.FlattenedFiles);
    }

    [Fact]
    public void MoveOrCopyStructureFileUsesCollisionPathForSameSizedDifferentContent()
    {
        using var temp = TempProject.Create();
        var moveRoot = Path.Combine(temp.Root, "project", "Haltungen_Verteilt");
        var source = Path.Combine(moveRoot, "06.1-2", ModernizerLegacyFolders.HoldingVideo, "video.mp4");
        var target = Path.Combine(moveRoot, "06.1-2", "20250101_06.1-2.mp4");
        var expectedCollision = Path.Combine(moveRoot, "06.1-2", "20250101_06.1-2_1.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(source, "source-01");
        File.WriteAllText(target, "target-02");

        var report = new ModernizeReport();
        var result = ModernizerStructureFileMover.MoveOrCopyStructureFile(
            source,
            target,
            moveRoot,
            dryRun: false,
            report,
            StructureMoveKind.FlatMedia);

        Assert.Equal(expectedCollision, result);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(expectedCollision));
        Assert.Equal("source-01", File.ReadAllText(source));
        Assert.Equal("target-02", File.ReadAllText(target));
        Assert.Equal("source-01", File.ReadAllText(expectedCollision));
        Assert.Equal(0, report.ReusedFiles);
        Assert.Equal(1, report.FlattenedFiles);
    }
}
