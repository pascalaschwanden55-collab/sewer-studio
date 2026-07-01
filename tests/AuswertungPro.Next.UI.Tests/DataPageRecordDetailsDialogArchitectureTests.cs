using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordDetailsDialogArchitectureTests
{
    [Fact]
    public void DataPage_delegiert_haltungs_detail_dialog_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var dataPage = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordDetailsDialogController.cs"));

        Assert.Contains("private readonly DataPageRecordDetailsDialogController _recordDetailsDialogController;", dataPage, StringComparison.Ordinal);
        Assert.Contains("new DataPageRecordDetailsDialogController(", dataPage, StringComparison.Ordinal);
        Assert.Contains("new DataPageRecordDetailsDialogRequest(", controller, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            dataPage,
            "private void ShowHaltungRecordDetails(HaltungRecord record)");

        Assert.Contains("_recordDetailsDialogController.Build(record)", method, StringComparison.Ordinal);
        Assert.Contains("ShowRecordDetailsWindow(request);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new RecordDetailsWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Haltung {holding}", method, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildHaltungRecordDetails(record)", method, StringComparison.Ordinal);
    }
}
