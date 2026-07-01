using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageClearColumnArchitectureTests
{
    [Fact]
    public void DataPage_delegates_clear_column_logic_to_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var method = SourceTextTestHelpers.ExtractMethodBody(source, "private void ClearColumn(string fieldName, string displayName)");

        Assert.Contains("DataPageClearColumnController.BuildPlan(", method, StringComparison.Ordinal);
        Assert.Contains("DataPageClearColumnController.ClearColumn(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("record.SetFieldValue(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("record.FieldMeta.TryGetValue", method, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.Records.Count(r", method, StringComparison.Ordinal);
    }
}
