using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageToolbarLayoutTests
{
    [Fact]
    public void AnsichtDropdown_contains_grid_display_controls()
    {
        var xaml = ReadDataPageXaml();
        var menu = ExtractContextMenu(xaml, "x:Name=\"AnsichtDropdown\"");

        Assert.Contains("Zeilenhöhe:", menu);
        Assert.Contains("GridMinRowHeight", menu);
        Assert.Contains("Zoom:", menu);
        Assert.Contains("GridZoom", menu);
        Assert.Contains("Ausrichtung:", menu);
        Assert.Contains("AlignLeftButton", menu);
        Assert.Contains("AlignBottomButton", menu);
    }

    [Fact]
    public void HydraulikActions_are_grouped_in_one_dropdown()
    {
        var xaml = ReadDataPageXaml();
        var menu = ExtractContextMenu(xaml, "x:Name=\"HydraulikDropdown\"");

        Assert.Contains("HydraulikMenu_Click", menu);
        Assert.Contains("HydraulikPrint_Click", menu);
        Assert.DoesNotContain("x:Name=\"HydraulikButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"HydraulikPrintButton\"", xaml);
    }

    private static string ReadDataPageXaml()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml");
        return File.ReadAllText(path);
    }

    private static string ExtractContextMenu(string xaml, string ownerMarker)
    {
        var ownerStart = xaml.IndexOf(ownerMarker, StringComparison.Ordinal);
        Assert.True(ownerStart >= 0, $"Owner marker not found: {ownerMarker}");

        var contextStart = xaml.IndexOf("<Button.ContextMenu>", ownerStart, StringComparison.Ordinal);
        Assert.True(contextStart >= 0, $"Context menu not found after: {ownerMarker}");

        var contextEnd = xaml.IndexOf("</Button.ContextMenu>", contextStart, StringComparison.Ordinal);
        Assert.True(contextEnd >= 0, $"Context menu end not found after: {ownerMarker}");

        return xaml.Substring(contextStart, contextEnd - contextStart);
    }

}
