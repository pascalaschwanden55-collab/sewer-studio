using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExplorerRevealServiceTests
{
    [Fact]
    public void BuildStartInfo_SelectsExistingFileInExplorer()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        File.WriteAllText(file, "x");
        try
        {
            var plan = ExplorerRevealService.BuildStartInfo(file);

            Assert.True(plan.Success, plan.Error);
            Assert.NotNull(plan.StartInfo);
            Assert.Equal("explorer.exe", plan.StartInfo.FileName);
            Assert.Equal($"/select,\"{Path.GetFullPath(file)}\"", plan.StartInfo.Arguments);
            Assert.False(plan.StartInfo.UseShellExecute);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void BuildStartInfo_OpensExistingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var plan = ExplorerRevealService.BuildStartInfo(directory);

            Assert.True(plan.Success, plan.Error);
            Assert.NotNull(plan.StartInfo);
            Assert.Equal("explorer.exe", plan.StartInfo.FileName);
            Assert.Equal($"\"{Path.GetFullPath(directory)}\"", plan.StartInfo.Arguments);
            Assert.False(plan.StartInfo.UseShellExecute);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
