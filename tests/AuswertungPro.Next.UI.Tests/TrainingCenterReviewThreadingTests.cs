using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewThreadingTests
{
    [Fact]
    public void ReviewFreigabe_LaedtSamplesNurUeberUiDispatcher()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));
        var normalized = source.Replace("\r\n", "\n");

        Assert.Contains(
            "OnUi(() =>\n        {\n            ObservableCollectionContentController.ReplaceWith(Samples, list);",
            normalized);
    }

    [Fact]
    public void StartdatenSammelfreigabe_NutztDispatcherSnapshotDerReviewQueue()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));
        var normalized = source.Replace("\r\n", "\n");

        Assert.Contains("GetProtocolStartdataReviewItems()", source);
        Assert.DoesNotContain("ReviewQueue\n            .Where", normalized);
    }

}
