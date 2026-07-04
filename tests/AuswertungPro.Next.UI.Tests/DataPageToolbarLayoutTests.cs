using System.IO;
using System.Text.RegularExpressions;
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
        AssertNoForbiddenTokens(
            xaml,
            "x:Name=\"HydraulikButton\"",
            "x:Name=\"HydraulikPrintButton\"");
    }

    [Fact]
    public void Haltungsansicht_lives_in_main_grid_row_and_uses_haltung_search_label()
    {
        var xaml = ReadDataPageXaml();
        var ansichtXaml = ReadHaltungsansichtXaml();

        Assert.Contains("Text=\"Suche Haltung:\"", xaml);
        Assert.DoesNotContain("Text=\"Suche Schacht:\"", xaml);

        var match = Regex.Match(xaml, @"<haltung:HaltungsansichtView\b[^>]*/>", RegexOptions.Singleline);
        Assert.True(match.Success, "HaltungsansichtView not found in DataPage.xaml");
        Assert.Contains("Grid.Row=\"1\"", match.Value);

        Assert.Contains("x:Key=\"HaltungListItemStyle\"", ansichtXaml);
        Assert.Contains("ItemContainerStyle=\"{StaticResource HaltungListItemStyle}\"", ansichtXaml);
        Assert.Contains("Property=\"IsSelected\"", ansichtXaml);
        Assert.Contains("Property=\"IsMouseOver\"", ansichtXaml);
    }

    private static string ReadDataPageXaml()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml");
        return File.ReadAllText(path);
    }

    private static string ReadHaltungsansichtXaml()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml");
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

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte Toolbar-Buttons gefunden: " + string.Join(", ", hits));
    }
}
