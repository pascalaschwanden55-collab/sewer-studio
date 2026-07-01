using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageComboBoxCommitArchitectureTests
{
    [Fact]
    public void DataPage_delegiert_combobox_commit_logik_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var dataPage = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageComboBoxCommitController.cs"));

        Assert.Contains("DataPageComboBoxCommitController.Commit(", dataPage, StringComparison.Ordinal);
        Assert.Contains("public static DataPageComboBoxCommitResult Commit", controller, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            dataPage,
            "private void CommitComboBoxValue(ComboBox? combo)");

        Assert.Contains("DataPageComboBoxCommitController.Commit(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("record.SetFieldValue", method, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.EnsureOptionForField(fieldName", method, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.ScheduleAutoSave();", method, StringComparison.Ordinal);
        Assert.DoesNotContain("!vm.IsProjectReady", method, StringComparison.Ordinal);
    }
}
