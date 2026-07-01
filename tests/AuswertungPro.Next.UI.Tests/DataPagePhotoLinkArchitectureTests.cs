using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePhotoLinkArchitectureTests
{
    [Fact]
    public void DataPage_delegiert_foto_link_logik_an_controller()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var dataPage = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPagePhotoLinkController.cs"));

        Assert.Contains("DataPagePhotoLinkController.BuildOpenPlan(", dataPage, StringComparison.Ordinal);
        Assert.Contains("public static DataPagePhotoLinkOpenPlan BuildOpenPlan", controller, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            dataPage,
            "private void OpenPhotoLink_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("DataPagePhotoLinkController.BuildOpenPlan(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveFilePath(rawPath", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(resolved", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var resolved =", method, StringComparison.Ordinal);
    }
}
