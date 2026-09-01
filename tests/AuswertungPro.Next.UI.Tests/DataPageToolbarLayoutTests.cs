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

    [Fact]
    public void Haltungs_und_Schachtansicht_use_scrollable_section_columns_with_vertical_fields()
    {
        var ansichtXaml = ReadHaltungsansichtXaml();
        var detailsXaml = ReadRecordDetailsXaml();
        var schachtansichtXaml = ReadUiXaml("Views", "Pages", "Schachtansicht", "SchachtansichtView.xaml");
        var detailWindowXaml = ReadUiXaml("Views", "Windows", "RecordDetailsWindow.xaml");

        var detail = Regex.Match(
            ansichtXaml,
            @"<controls:RecordDetailsView\b[^>]*/>",
            RegexOptions.Singleline);

        Assert.True(detail.Success, "RecordDetailsView not found in HaltungsansichtView.xaml");
        Assert.Contains("IsCompactLayout=\"True\"", detail.Value, StringComparison.Ordinal);
        Assert.Contains("IsCompactLayout=\"True\"", schachtansichtXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCompactLayout=\"True\"", detailWindowXaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("PanningMode=\"VerticalOnly\"", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"CompactDetailGroupPanel\"", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<ItemsPanelTemplate x:Key=\"CompactDetailFieldPanel\"><StackPanel Orientation=\"Vertical\"/></ItemsPanelTemplate>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ItemsPanel\" Value=\"{StaticResource CompactDetailGroupPanel}\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ItemsPanel\" Value=\"{StaticResource CompactDetailFieldPanel}\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"CompactDetailFieldContainerStyle\"", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ItemContainerStyle\" Value=\"{StaticResource CompactDetailFieldContainerStyle}\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Width\" Value=\"Auto\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Setter Property=\"Width\" Value=\"200\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0,0,0,6\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"8\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"72\"/>", detailsXaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Spalten teilen sich die volle Breite gleichmaessig.
    ///
    /// Frueher standen hier zwei feste Breiten (226 px) und eine feste Zuordnung
    /// Gruppenart -> Spaltennummer. Beides passt nicht mehr, seit der Benutzer selbst
    /// bestimmt, welches Feld in welcher Spalte liegt: zieht er "Bemerkungen" in die
    /// Stammdaten, waere deren feste schmale Spalte falsch. Und eine Spalte ohne
    /// sichtbare Karte faellt ganz weg - eine feste Spaltennummer zeigte dann ins Leere.
    /// </summary>
    [Fact]
    public void Compact_detail_groups_share_the_full_width_evenly()
    {
        var detailsXaml = ReadRecordDetailsXaml();
        var panel = Regex.Match(
            detailsXaml,
            @"<ItemsPanelTemplate x:Key=""CompactDetailGroupPanel"">(?<content>.*?)</ItemsPanelTemplate>",
            RegexOptions.Singleline);
        var containerStyle = Regex.Match(
            detailsXaml,
            @"<Style x:Key=""CompactDetailGroupContainerStyle"".*?</Style>",
            RegexOptions.Singleline);

        Assert.True(panel.Success, "CompactDetailGroupPanel not found in RecordDetailsView.xaml");

        // Eine Zeile, gleich breite Zellen: die Spaltenzahl folgt der Zahl der sichtbaren
        // Gruppen, und die Position in der Liste ist unmittelbar die Spalte.
        Assert.Contains("<UniformGrid Rows=\"1\"/>", panel.Groups["content"].Value, StringComparison.Ordinal);

        // Feste Breiten liessen rechts einen leeren Streifen stehen, sobald eine Spalte
        // weniger da ist.
        Assert.DoesNotContain("ColumnDefinition", panel.Groups["content"].Value, StringComparison.Ordinal);

        Assert.True(containerStyle.Success, "CompactDetailGroupContainerStyle not found in RecordDetailsView.xaml");

        // Keine feste Spaltennummer mehr - die Reihenfolge entscheidet.
        Assert.DoesNotContain("Grid.Column", containerStyle.Value, StringComparison.Ordinal);

        // Was bleibt: Dokumente stehen in der kompakten Ansicht aussen vor, und jede
        // Spalte fuellt ihre Zelle aus.
        Assert.Matches(@"(?s)RecordDetailGroupKind\.Documents.*?Visibility"" Value=""Collapsed""", containerStyle.Value);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\"/>", containerStyle.Value, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ItemContainerStyle\" Value=\"{StaticResource CompactDetailGroupContainerStyle}\"/>", detailsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Setter Property=\"Width\" Value=\"220\"/>", detailsXaml, StringComparison.Ordinal);
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

    private static string ReadRecordDetailsXaml()
        => ReadUiXaml("Views", "Controls", "RecordDetailsView.xaml");

    private static string ReadUiXaml(params string[] segments)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(segments).ToArray());
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
