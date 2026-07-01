using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewSamPersistenceTests
{
    [Fact]
    public void ReviewSamSegmentierung_wird_bis_zur_freigabe_gehalten()
    {
        var windowSource = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));
        var viewModelSource = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        Assert.Contains("Vm.PendingSamMask = CreateTrainingSegmentationMask(result.Response);", windowSource);
        Assert.Contains("wird mit Akzeptieren gespeichert", windowSource);
        Assert.Contains("TrainingSegmentationMask? PendingSamMask", viewModelSource);
        Assert.Contains("ApproveReviewItemAsync(item, feedback, ReviewQueueServiceRef, ct, box, mask)", viewModelSource);
    }

}
