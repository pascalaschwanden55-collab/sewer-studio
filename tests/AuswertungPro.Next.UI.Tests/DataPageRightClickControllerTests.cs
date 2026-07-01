using System.IO;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRightClickControllerTests
{
    [Fact]
    public void Resolve_returns_clear_column_when_clear_mode_has_column()
    {
        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: "Bemerkungen",
            columnDisplayName: "Bemerkungen",
            rowItem: new object());

        Assert.Equal(DataPageRightClickAction.ClearColumn, result.Action);
        Assert.Equal("Bemerkungen", result.FieldName);
        Assert.Equal("Bemerkungen", result.DisplayName);
        Assert.Null(result.RowItem);
    }

    [Fact]
    public void Resolve_prefers_row_selection_when_clear_mode_has_no_column()
    {
        var row = new object();

        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: null,
            columnDisplayName: null,
            rowItem: row);

        Assert.Equal(DataPageRightClickAction.SelectRow, result.Action);
        Assert.Same(row, result.RowItem);
    }

    [Fact]
    public void Resolve_returns_select_row_when_not_in_clear_mode()
    {
        var row = new object();

        var result = DataPageRightClickController.Resolve(
            clearColumnMode: false,
            columnFieldName: "Haltungsname",
            columnDisplayName: "Haltung",
            rowItem: row);

        Assert.Equal(DataPageRightClickAction.SelectRow, result.Action);
        Assert.Same(row, result.RowItem);
    }

    [Fact]
    public void Resolve_returns_none_without_clear_target_or_row()
    {
        var result = DataPageRightClickController.Resolve(
            clearColumnMode: false,
            columnFieldName: null,
            columnDisplayName: null,
            rowItem: null);

        Assert.Equal(DataPageRightClickAction.None, result.Action);
    }

    [Fact]
    public void DataPage_delegates_right_click_decision_to_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var method = SourceTextTestHelpers.ExtractMethodBody(source, "private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)");

        Assert.Contains("DataPageRightClickController.Resolve(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("if (ClearColumnMenuItem.IsChecked)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearColumn(fieldName, displayName)", method, StringComparison.Ordinal);
    }
}
