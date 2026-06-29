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
