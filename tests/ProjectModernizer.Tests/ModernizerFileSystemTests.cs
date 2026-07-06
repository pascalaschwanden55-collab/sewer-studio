using Xunit;

public sealed class ModernizerFileSystemTests
{
    [Fact]
    public void SameFileContentReturnsFalseForSameSizedDifferentContent()
    {
        using var temp = TempProject.Create();
        var left = Path.Combine(temp.Root, "left.txt");
        var right = Path.Combine(temp.Root, "right.txt");
        File.WriteAllText(left, "source-01");
        File.WriteAllText(right, "target-02");

        var same = ModernizerFileSystem.SameFileContent(left, right);

        Assert.False(same);
    }

    [Fact]
    public void CopyFileExactUsesCollisionPathForSameSizedDifferentContent()
    {
        using var temp = TempProject.Create();
        var source = Path.Combine(temp.Root, "source.txt");
        var target = Path.Combine(temp.Root, "target.txt");
        var collision = Path.Combine(temp.Root, "target_1.txt");
        File.WriteAllText(source, "source-01");
        File.WriteAllText(target, "target-02");
        var report = new ModernizeReport();

        var copied = ModernizerFileSystem.CopyFileExact(source, target, dryRun: false, report, FileCopyKind.Import);

        Assert.Equal(collision, copied);
        Assert.True(File.Exists(source));
        Assert.Equal("target-02", File.ReadAllText(target));
        Assert.Equal("source-01", File.ReadAllText(collision));
        Assert.Equal(1, report.ImportCopied);
        Assert.Equal(0, report.ReusedFiles);
    }

    [Fact]
    public void EmptyDirectoryCleanerDeletesNestedEmptyDirectoryTree()
    {
        using var temp = TempProject.Create();
        var root = Path.Combine(temp.Root, "PDF");
        Directory.CreateDirectory(Path.Combine(root, "nested"));

        var deleted = ModernizerEmptyDirectoryCleaner.TryDeleteDirectoryTreeIfEmpty(root, dryRun: false);

        Assert.True(deleted);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void EmptyDirectoryCleanerKeepsDirectoryTreeWithFiles()
    {
        using var temp = TempProject.Create();
        var root = Path.Combine(temp.Root, "PDF");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        File.WriteAllText(Path.Combine(root, "nested", "keep.pdf"), "pdf");

        var deleted = ModernizerEmptyDirectoryCleaner.TryDeleteDirectoryTreeIfEmpty(root, dryRun: false);

        Assert.False(deleted);
        Assert.True(Directory.Exists(root));
    }
}
