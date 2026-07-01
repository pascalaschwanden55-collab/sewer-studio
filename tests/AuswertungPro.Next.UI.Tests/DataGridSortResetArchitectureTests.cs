using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridSortResetArchitectureTests
{
    [Fact]
    public void DataPage_delegates_sort_reset_to_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var method = SourceTextTestHelpers.ExtractMethodBody(source, "private void ResetSort()");

        Assert.Contains("DataGridSortResetController.Reset(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SortDescriptions", method, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomSort", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SortDirection = null", method, StringComparison.Ordinal);
    }
}
