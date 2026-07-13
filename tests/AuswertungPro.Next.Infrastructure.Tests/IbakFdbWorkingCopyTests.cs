using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class IbakFdbWorkingCopyTests
{
    [Fact]
    public void Create_isolates_changes_from_original_and_dispose_removes_copy()
    {
        using var sourceDirectory = new TempDirectory();
        var sourcePath = Path.Combine(sourceDirectory.Path, "Arizona.fdb");
        File.WriteAllText(sourcePath, "original-database-content");

        string copyPath;
        using (var workingCopy = IbakFdbWorkingCopy.Create(sourcePath))
        {
            copyPath = workingCopy.DatabasePath;

            Assert.NotEqual(Path.GetFullPath(sourcePath), Path.GetFullPath(copyPath));
            Assert.Equal("original-database-content", File.ReadAllText(copyPath));

            File.WriteAllText(copyPath, "firebird-may-change-this-copy");
            Assert.Equal("original-database-content", File.ReadAllText(sourcePath));
        }

        Assert.False(File.Exists(copyPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(copyPath)));
    }

    [Fact]
    public void Create_makes_readonly_source_copy_writable_without_changing_source_attribute()
    {
        using var sourceDirectory = new TempDirectory();
        var sourcePath = Path.Combine(sourceDirectory.Path, "Arizona.fdb");
        File.WriteAllText(sourcePath, "database");
        File.SetAttributes(sourcePath, File.GetAttributes(sourcePath) | FileAttributes.ReadOnly);

        try
        {
            using var workingCopy = IbakFdbWorkingCopy.Create(sourcePath);

            Assert.False(File.GetAttributes(workingCopy.DatabasePath).HasFlag(FileAttributes.ReadOnly));
            Assert.True(File.GetAttributes(sourcePath).HasFlag(FileAttributes.ReadOnly));
        }
        finally
        {
            File.SetAttributes(sourcePath, File.GetAttributes(sourcePath) & ~FileAttributes.ReadOnly);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
