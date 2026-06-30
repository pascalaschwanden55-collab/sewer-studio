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

    // Hinweis: Im integrierten Stand behält DataPage bewusst die (gesmokte) Mainline-View
    // ohne x1's Alignment-Toolbar-Auslagerung; nur SchaechtePage nutzt die x1-Auslagerung.
    // Daher prüft dieser Test die Alignment-Delegation ausschließlich für SchaechtePage.
    [Fact]
    public void SchaechtePage_delegates_alignment_toolbar_state()
    {
        var root = FindRepositoryRoot();
        var schaechtePageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("DataGridColumnAlignmentToolbar", schaechtePageSource);
        Assert.DoesNotContain("_updatingAlignmentButtons", schaechtePageSource);
        Assert.DoesNotContain("DataGridColumn? _activeColumn", schaechtePageSource);
        Assert.DoesNotContain("private DataGridColumn? GetActiveColumn", schaechtePageSource);
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
