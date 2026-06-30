using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageColumnLayoutRefactorTests
{
    [Fact]
    public void SchaechtePage_uses_shared_column_layout_controller_instead_of_local_layout_state()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("DataGridColumnLayoutController", source);
        Assert.DoesNotContain("_columnHorizontalAlignments", source);
        Assert.DoesNotContain("_columnVerticalAlignments", source);
        Assert.DoesNotContain("_baseCellStyles", source);
        Assert.DoesNotContain("_baseTextElementStyles", source);
        Assert.DoesNotContain("_baseTextEditingStyles", source);
        Assert.DoesNotContain("ParseHorizontalAlignment", source);
        Assert.DoesNotContain("ParseVerticalAlignment", source);
    }

    [Fact]
    public void DataPage_and_SchaechtePage_delegate_alignment_toolbar_state()
    {
        var root = FindRepositoryRoot();
        var dataPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.ColumnLayout.cs"));
        var dataPageRootSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var schaechtePageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("DataGridColumnAlignmentToolbar", dataPageRootSource);
        Assert.Contains("DataGridColumnAlignmentToolbar", schaechtePageSource);
        Assert.DoesNotContain("_updatingAlignmentButtons", dataPageRootSource);
        Assert.DoesNotContain("_updatingAlignmentButtons", schaechtePageSource);
        Assert.DoesNotContain("DataGridColumn? _activeColumn", dataPageRootSource);
        Assert.DoesNotContain("DataGridColumn? _activeColumn", schaechtePageSource);
        Assert.DoesNotContain("private DataGridColumn? GetActiveColumn", dataPageSource);
        Assert.DoesNotContain("private DataGridColumn? GetActiveColumn", schaechtePageSource);
        Assert.DoesNotContain("private void SetAlignmentButtonsUnchecked", dataPageSource);
        Assert.DoesNotContain("private void SetAlignmentButtonsUnchecked", schaechtePageSource);
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
