using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageBeobachtungenArchitectureTests
{
    [Fact]
    public void DataPage_delegiert_beobachtungen_menu_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var dataPage = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageBeobachtungenController.cs"));

        Assert.Contains("private readonly DataPageBeobachtungenController _beobachtungenController;", dataPage, StringComparison.Ordinal);
        Assert.Contains("new DataPageBeobachtungenController(", dataPage, StringComparison.Ordinal);
        Assert.Contains("public DataPageBeobachtungenWindowRequest? BuildOpenRequest", controller, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            dataPage,
            "private void BeobachtungenMenu_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("_beobachtungenController.BuildOpenRequest(", method, StringComparison.Ordinal);
        Assert.Contains("ShowOrUpdateBeobachtungenWindow(request);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("sp.Vsa.EvaluateRecord", method, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.SyncObservationsToHoldingFields(record", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Dialog", method, StringComparison.Ordinal);
    }
}
