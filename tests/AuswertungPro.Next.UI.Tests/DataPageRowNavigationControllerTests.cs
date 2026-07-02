using System.IO;
using AuswertungPro.Next.UI.DataPage;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRowNavigationControllerTests
{
    [Fact]
    public void DataPage_position_and_row_handlers_delegate_to_navigation_controller()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var handlers = ExtractBetween(
            source,
            "private void MoveToPosition_Click",
            "private static HaltungRecord? ResolveActionRecord");

        Assert.Contains("DataPageRowNavigationController.TryMoveToPosition", handlers);
        Assert.Contains("DataPageRowNavigationController.TryResolveRowIndex", handlers);
        Assert.DoesNotContain("int.TryParse(MoveToPositionBox.Text.Trim()", handlers);
        Assert.DoesNotContain("int.TryParse(GoToRowBox.Text.Trim()", handlers);
        Assert.DoesNotContain("idx = vm.Records.Count - 1", handlers);
    }

    [Fact]
    public void TryMoveToPosition_parses_position_and_calls_view_model_move()
    {
        var movedTo = 0;
        var dialogs = new List<(string Message, string Title)>();

        var moved = DataPageRowNavigationController.TryMoveToPosition(
            " 12 ",
            pos =>
            {
                movedTo = pos;
                return true;
            },
            (message, title) => dialogs.Add((message, title)));

        Assert.True(moved);
        Assert.Equal(12, movedTo);
        Assert.Empty(dialogs);
    }

    [Fact]
    public void TryMoveToPosition_reports_invalid_number_without_moving()
    {
        var moved = false;
        var dialogs = new List<(string Message, string Title)>();

        var result = DataPageRowNavigationController.TryMoveToPosition(
            "abc",
            _ =>
            {
                moved = true;
                return true;
            },
            (message, title) => dialogs.Add((message, title)));

        Assert.False(result);
        Assert.False(moved);
        Assert.Equal(("Bitte eine gueltige Zahl eingeben.", "Position"), Assert.Single(dialogs));
    }

    [Fact]
    public void TryMoveToPosition_reports_when_view_model_rejects_move()
    {
        var dialogs = new List<(string Message, string Title)>();

        var result = DataPageRowNavigationController.TryMoveToPosition(
            "3",
            _ => false,
            (message, title) => dialogs.Add((message, title)));

        Assert.False(result);
        Assert.Equal(("Verschieben nicht moeglich. Bitte Zeile auswaehlen.", "Position"), Assert.Single(dialogs));
    }

    [Theory]
    [InlineData("1", 4, 0)]
    [InlineData(" 2 ", 4, 1)]
    [InlineData("99", 4, 3)]
    public void TryResolveRowIndex_returns_zero_based_clamped_index(string text, int recordCount, int expectedIndex)
    {
        var dialogs = new List<(string Message, string Title)>();

        var resolved = DataPageRowNavigationController.TryResolveRowIndex(
            text,
            recordCount,
            (message, title) => dialogs.Add((message, title)),
            out var rowIndex);

        Assert.True(resolved);
        Assert.Equal(expectedIndex, rowIndex);
        Assert.Empty(dialogs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void TryResolveRowIndex_reports_invalid_row_number(string text)
    {
        var dialogs = new List<(string Message, string Title)>();

        var resolved = DataPageRowNavigationController.TryResolveRowIndex(
            text,
            recordCount: 4,
            (message, title) => dialogs.Add((message, title)),
            out var rowIndex);

        Assert.False(resolved);
        Assert.Equal(-1, rowIndex);
        Assert.Equal(("Bitte eine gueltige Zeilennummer eingeben.", "Gehe zu Zeile"), Assert.Single(dialogs));
    }

    [Fact]
    public void TryResolveRowIndex_returns_false_without_dialog_when_grid_is_empty()
    {
        var dialogs = new List<(string Message, string Title)>();

        var resolved = DataPageRowNavigationController.TryResolveRowIndex(
            "1",
            recordCount: 0,
            (message, title) => dialogs.Add((message, title)),
            out var rowIndex);

        Assert.False(resolved);
        Assert.Equal(-1, rowIndex);
        Assert.Empty(dialogs);
    }

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found: {endMarker}");
        return source[start..end];
    }
}
