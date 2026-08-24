using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossiersPageContextMenuTests
{
    [Fact]
    public void Haltungsmenue_zeigt_Video_Protokoll_und_Zur_Haltung_in_dieser_Reihenfolge()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));
        var start = xaml.IndexOf("<DataGrid.ContextMenu>", StringComparison.Ordinal);
        var end = xaml.IndexOf("</DataGrid.ContextMenu>", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var menu = xaml[start..end];
        var video = menu.IndexOf("Header=\"Video abspielen\"", StringComparison.Ordinal);
        var protocol = menu.IndexOf("Header=\"Haltungsprotokoll (PDF) öffnen…\"", StringComparison.Ordinal);
        var navigate = menu.IndexOf("Header=\"Zur Haltung\"", StringComparison.Ordinal);

        Assert.True(video >= 0 && video < protocol);
        Assert.True(protocol < navigate);
        Assert.Equal(3, Regex.Matches(menu, "<MenuItem Header=").Count);
        Assert.Contains("PreviewMouseRightButtonDown=\"HoldingGrid_PreviewMouseRightButtonDown\"", xaml);
    }

    [Fact]
    public void Schachtmenue_zeigt_Protokoll_und_Zum_Schacht_in_dieser_Reihenfolge()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));
        var grid = xaml.IndexOf("x:Name=\"ShaftGrid\"", StringComparison.Ordinal);
        var start = xaml.IndexOf("<DataGrid.ContextMenu>", grid, StringComparison.Ordinal);
        var end = xaml.IndexOf("</DataGrid.ContextMenu>", start, StringComparison.Ordinal);

        Assert.True(grid >= 0 && start > grid && end > start);
        var menu = xaml[start..end];
        var protocol = menu.IndexOf(
            "Header=\"Schachtprotokoll (PDF) öffnen…\"",
            StringComparison.Ordinal);
        var navigate = menu.IndexOf("Header=\"Zum Schacht\"", StringComparison.Ordinal);

        Assert.True(protocol >= 0 && protocol < navigate);
        Assert.Equal(2, Regex.Matches(menu, "<MenuItem Header=").Count);
        Assert.Contains("PreviewMouseRightButtonDown=\"ShaftGrid_PreviewMouseRightButtonDown\"", xaml);
    }
}
