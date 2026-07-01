using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageHydraulikPanelArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_hydraulik_record_aufbereitung_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageHydraulikPanelController.cs"));

        Assert.Contains("DataPageHydraulikPanelController.BuildOpenRequest(", viewModel, StringComparison.Ordinal);
        Assert.Contains("public static DataPageHydraulikPanelRequest BuildOpenRequest", controller, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            viewModel,
            "private void OpenHydraulikPanel(HaltungRecord? record)");

        Assert.Contains("DataPageHydraulikPanelController.BuildOpenRequest(record)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DnValueParser.TryParseMillimeters", method, StringComparison.Ordinal);
        Assert.DoesNotContain("record.GetFieldValue(\"Rohrmaterial\")", method, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.LoadFromRecord", method, StringComparison.Ordinal);
    }
}
