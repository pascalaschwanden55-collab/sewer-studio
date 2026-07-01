using System;
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

        var method = ExtractMethod(source, "private async Task LoadSamplesInternalAsync()");

        Assert.Contains("OnUi(() =>", method);
        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Samples, list)", method);
        Assert.True(
            method.IndexOf("OnUi(() =>", StringComparison.Ordinal)
            < method.IndexOf("ObservableCollectionContentController.ReplaceWith(Samples, list)", StringComparison.Ordinal),
            "Samples-Replace muss ueber den UI-Dispatcher laufen; Review-Freigaben koennen nach ConfigureAwait(false) auf einem Hintergrundthread fortsetzen.");
    }

    [Fact]
    public void StartdatenSammelfreigabe_NutztDispatcherSnapshotDerReviewQueue()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        var method = ExtractMethod(source, "public async Task ApproveAllStartdataAsync(CancellationToken ct = default)");

        Assert.Contains("GetProtocolStartdataReviewItems()", method);
        Assert.DoesNotContain("ReviewQueue\r\n            .Where", method);
        Assert.DoesNotContain("ReviewQueue\n            .Where", method);
    }

}
