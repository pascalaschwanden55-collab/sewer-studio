using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnAlignmentArchitectureTests
{
    [Fact]
    public void DataPage_delegates_column_alignment_to_shared_toolbar()
    {
        var page = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var columnLayout = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.ColumnLayout.cs"));

        Assert.Contains(
            "private readonly DataGridColumnAlignmentToolbar _columnAlignmentToolbar;",
            page,
            StringComparison.Ordinal);
        Assert.Contains("_columnAlignmentToolbar.ClearActiveColumn();", page, StringComparison.Ordinal);
        Assert.Contains("_columnAlignmentToolbar.SetAlignment(", page, StringComparison.Ordinal);
        Assert.Contains("_columnAlignmentToolbar.TrackSelectedCells();", columnLayout, StringComparison.Ordinal);
        Assert.Contains("_columnAlignmentToolbar.TrackCurrentCell();", columnLayout, StringComparison.Ordinal);
        Assert.Contains("_columnAlignmentToolbar.TrackHeaderClick(dep);", columnLayout, StringComparison.Ordinal);
        Assert.Contains(
            "_columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Left);",
            columnLayout,
            StringComparison.Ordinal);
        Assert.Contains(
            "_columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Bottom);",
            columnLayout,
            StringComparison.Ordinal);

        Assert.DoesNotContain("private DataGridColumn? _activeColumn;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _updatingAlignmentButtons;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetActiveColumn(", columnLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("SetAlignmentButtonsUnchecked(", columnLayout, StringComparison.Ordinal);
    }
}
