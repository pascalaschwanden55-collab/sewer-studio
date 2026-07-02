using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerCollectionDispatchTests
{
    [Fact]
    public void Collection_changed_handlers_schedule_tile_rendering_without_invoke_async()
    {
        var source = File.ReadAllText(RepoFile(
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

}
