using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageMediaSearchArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_medien_suche_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethodBody(source, "public void OpenMediaSearchWindow()");

        Assert.Contains("_mediaSearchController.Open();", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new MediaSearchWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("AppliedVideoCount", method, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPropertyChanged(nameof(Records))", method, StringComparison.Ordinal);
        Assert.DoesNotContain("LastVideoSourceFolder", method, StringComparison.Ordinal);
    }

}
