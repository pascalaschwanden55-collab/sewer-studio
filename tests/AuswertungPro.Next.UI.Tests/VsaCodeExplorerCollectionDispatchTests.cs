using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerCollectionDispatchTests
{
    [Fact]
    public void Collection_changed_handlers_schedule_tile_rendering_without_invoke_async()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VsaCodeExplorerWindow.xaml.cs"));

        Assert.DoesNotContain("Dispatcher.InvokeAsync(() => RenderColumnTiles", source);
        Assert.Contains("Dispatcher.BeginInvoke(new Action(() => RenderColumnTiles(GroupList", source);
        Assert.Contains("Dispatcher.BeginInvoke(new Action(() => RenderColumnTiles(CodeList", source);
        Assert.Contains("Dispatcher.BeginInvoke(new Action(() => RenderColumnTiles(Char1List", source);
        Assert.Contains("Dispatcher.BeginInvoke(new Action(() => RenderColumnTiles(Char2List", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
