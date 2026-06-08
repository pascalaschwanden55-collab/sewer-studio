using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewSamPersistenceTests
{
    [Fact]
    public void ReviewSamSegmentierung_wird_bis_zur_freigabe_gehalten()
    {
        var windowSource = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));
        var viewModelSource = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        Assert.Contains("Vm.PendingSamMask = CreateTrainingSegmentationMask(result.Response);", windowSource);
        Assert.Contains("wird mit Akzeptieren gespeichert", windowSource);
        Assert.Contains("TrainingSegmentationMask? PendingSamMask", viewModelSource);
        Assert.Contains("ApproveReviewItemAsync(item, feedback, ReviewQueueServiceRef, ct, box, mask)", viewModelSource);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
